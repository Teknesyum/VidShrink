using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia.Threading;
using VidShrink.App.Localization;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class MainWindow
{
    /// <summary>
    /// Yoklamayı arayüz iş parçacığından ayıran geçit.
    ///
    /// Okuma tarafı süreç doğurmaz: yalnız ısıtılmış cevabı verir. Sorulan kodlayıcı henüz
    /// ölçülmemişse <see cref="IEncoderAvailability.EncoderState"/> üzerinden "ölçülmedi" der —
    /// "çalışmıyor" demez — ve ölçümü arka planda kuyruğa alır. Ölçüm bitince
    /// <c>onMeasured</c> çağrılır ve hesap yenilenir. Aynı kodlayıcı aynı anda iki kez
    /// kuyruğa girmez; N yeniden hesap N yoklama doğurmaz.
    ///
    /// <see cref="FfmpegArguments.CachedPsychovisualArgs"/> ile
    /// <see cref="FfmpegArguments.WarmPsychovisual"/> ayrımının aynısı: ölçen yol ayrı,
    /// okuyan yol saf.
    /// </summary>
    internal sealed class DeferredEncoderAvailability
        : IEncoderAvailability, IHdr10EncoderAvailability, IEncoderOptionAvailability, IHdr10ProbeAvailability
    {
        /// <summary>
        /// Bu süreden uzun süren yoklama yerleşmiş sayılmaz. Yükün altında aynı komut
        /// 3625-14855 ms sürebiliyor (docs/olcumler/handbrake-acigi.md); o anki cevabı
        /// kalıcı kabul etmek T94'ün kaldırdığı "geçici düşüş kalıcı donanım yok kararı"
        /// kusurunu geri getirirdi.
        /// </summary>
        internal const int UnsettledProbeMs = 2000;

        /// <summary>
        /// Yerleşmeyen bir yoklama art arda en çok bu kadar denenir; sonrası
        /// <see cref="RetryAfterFailureMs"/> beklemeye tabidir.
        /// </summary>
        internal const int MaxAttempts = 2;

        /// <summary>
        /// Bir kodlayıcı için toplam yoklama tavanı. Bekleme sonrası yeniden deneme kolu
        /// <see cref="MaxAttempts"/>i aşabiliyordu ve <b>tavanı yoktu</b>: yerleşmeyen bir
        /// yoklama oturum boyunca her <see cref="RetryAfterFailureMs"/> ms'de bir yeni
        /// ffmpeg doğuruyordu. Tavan, geçici bir arızadan bir kez toparlanmaya izin verir
        /// (iki hızlı deneme + bir beklemeli deneme); ondan sonra cevap <b>bilinmeyen</b>
        /// kalır ve <see cref="Unsettled"/> ile arayüze taşınır.
        /// </summary>
        internal const int MaxTotalAttempts = 3;

        internal const int RetryAfterFailureMs = 5000;

        private sealed class Answer
        {
            internal EncoderProbeState State = EncoderProbeState.Unmeasured;
            internal bool Works;
            internal string? PixelFormat;
            internal bool Settled;
            internal int Attempts;
            internal string? Failure;
            internal long ElapsedMs = -1;
            internal long LastAttemptTicks;
        }

        /// <summary>
        /// Bir kodlayicinin yoklama cevabinin hangi durumda oldugu. <c>NotWorking</c> ile
        /// <c>Failed</c> ayri: birincisi olculmus bir cevap, ikincisi yoklamanin hic cevap
        /// uretememesi. Ikisini ayni yere yazmak ucuncu durumu yok ediyordu.
        /// </summary>
        internal enum ProbeAnswer
        {
            Unknown,
            Working,
            NotWorking,
            Unsettled,
            Failed,

            /// <summary>
            /// Yoklama koştu, hızlı döndü ve <b>sonuca varamadı</b>: ffmpeg zaman aşımına
            /// uğradı ya da süreç hiç başlayamadı. <c>Unsettled</c>dan ayrı, çünkü o "çok
            /// uzun sürdü" demek; bu, süresi ne olursa olsun "cevap yok" demek.
            /// </summary>
            Unmeasured
        }

        private readonly IEncoderAvailability _source;
        private readonly Action _onMeasured;
        private readonly object _gate = new();
        private readonly Dictionary<string, Answer> _answers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
        private int _probes;

        internal DeferredEncoderAvailability(IEncoderAvailability source, Action onMeasured)
        {
            _source = source;
            _onMeasured = onMeasured;
        }

        /// <summary>Ölçümün gerçekten koştuğu yetenek nesnesi. Kodlama yolu bunu kullanır.</summary>
        internal IEncoderAvailability Source => _source;

        /// <summary>Arka planda koşan yoklama var mı.</summary>
        internal bool Pending
        {
            get { lock (_gate) return _running.Count > 0; }
        }

        /// <summary>Bu geçidin bugüne kadar kaç yoklama başlattığı. Ölçü bunu pinler.</summary>
        internal int Probes
        {
            get { lock (_gate) return _probes; }
        }

        /// <summary>
        /// Denemesi bittiği hâlde yerleşmemiş bir yoklama var mı. Varsa hesap bilinmeyen
        /// bir cevapla çalışıyor ve bunun kullanıcıya görünmesi gerekiyor.
        /// </summary>
        internal bool Unsettled
        {
            get
            {
                lock (_gate)
                    return _answers.Values.Any(a => !a.Settled && a.Attempts >= MaxAttempts);
            }
        }

        public bool HasEncoder(string name) => _source.HasEncoder(name);

        public bool SupportsEncoderOption(string codec, string option, string value)
            => _source is IEncoderOptionAvailability options && options.SupportsEncoderOption(codec, option, value);

        /// <summary>
        /// Kodlayıcının yoklaması yerleşti mi — ve yerleşmediyse <b>kuyruğa al</b>. Geçidin
        /// tetiği budur: sormak ölçümü başlatır. Ölçüler yoklamanın kaç kez koştuğunu
        /// buradan sayıyor.
        /// </summary>
        internal bool IsMeasured(string codec) => Ready(Key("works", codec), codec, hdr10: false);

        /// <inheritdoc cref="IsMeasured"/>
        internal bool IsHdr10Measured(string codec) => Ready(Key("hdr10", codec), codec, hdr10: true);

        public bool WorksAsEncoder(string codec)
        {
            lock (_gate) return _answers.TryGetValue(Key("works", codec), out var answer) && answer.Works;
        }

        /// <summary>
        /// Üç durumlu yüz. Sorulan kodlayıcı yerleşmemişse ölçüm burada kuyruğa girer;
        /// eskiden tetik ayrı bir <c>IEncoderMeasurementState</c> arayüzündeydi ve Core
        /// tarafı üç durumlu cevabı aldıktan <b>sonra</b> ikinci bir çağrıyla ölçümü
        /// başlatıyordu. Tetik cevabın yanına taşındı; geçici arayüz kalktı.
        /// </summary>
        public EncoderProbeState EncoderState(string codec)
        {
            var answer = AnswerFor(codec);
            if (answer is not (ProbeAnswer.Working or ProbeAnswer.NotWorking))
            {
                IsMeasured(codec);
                return EncoderProbeState.Unmeasured;
            }
            return answer == ProbeAnswer.Working ? EncoderProbeState.Working : EncoderProbeState.NotWorking;
        }

        /// <inheritdoc cref="EncoderState"/>
        public EncoderProbeState Hdr10State(string codec)
            => IsHdr10Measured(codec)
                ? Hdr10PixelFormat(codec) is not null ? EncoderProbeState.Working : EncoderProbeState.NotWorking
                : EncoderProbeState.Unmeasured;

        public string? Hdr10PixelFormat(string codec)
        {
            lock (_gate) return _answers.TryGetValue(Key("hdr10", codec), out var answer) ? answer.PixelFormat : null;
        }

        /// <summary>Kodlayicinin bugunku yoklama durumu. Olcu bunu okur, arayuz de.</summary>
        internal ProbeAnswer AnswerFor(string codec)
        {
            lock (_gate)
            {
                if (!_answers.TryGetValue(Key("works", codec), out var answer)) return ProbeAnswer.Unknown;
                if (answer.Failure is not null) return ProbeAnswer.Failed;
                if (answer.State == EncoderProbeState.Unmeasured) return ProbeAnswer.Unmeasured;
                if (!answer.Settled) return ProbeAnswer.Unsettled;
                return answer.State == EncoderProbeState.Working ? ProbeAnswer.Working : ProbeAnswer.NotWorking;
            }
        }

        /// <summary>
        /// Kodlayicinin son yoklamasinin gercekten kac ms surdugu, hic yoklanmadiysa -1.
        /// Yerlesme karari bu sureden turuyor; olcu ikisini yuzlestiriyor.
        /// </summary>
        internal long ElapsedMsFor(string codec)
        {
            lock (_gate) return _answers.TryGetValue(Key("works", codec), out var answer) ? answer.ElapsedMs : -1;
        }

        /// <summary>Yoklama firlattiysa istisnanin metni, yoksa <c>null</c>.</summary>
        internal string? FailureFor(string codec)
        {
            lock (_gate) return _answers.TryGetValue(Key("works", codec), out var answer) ? answer.Failure : null;
        }

        /// <summary>
        /// Yoklamasi istisnayla dusen ilk kodlayicinin istisna metni. Arayuz durum satiri
        /// bunu gosterir; istisna sessizce kaybolmaz.
        /// </summary>
        internal string? FirstFailure
        {
            get
            {
                lock (_gate) return _answers.Values.Select(a => a.Failure).FirstOrDefault(f => f is not null);
            }
        }

        private static string Key(string kind, string codec) => $"{kind}:{codec}";

        /// <summary>
        /// Yoklama yerleşmediyse cevap "ölçüldü" sayılmaz. Deneme sırası şu:
        /// <see cref="MaxAttempts"/> kadar art arda denenir, sonra
        /// <see cref="RetryAfterFailureMs"/> beklenip <b>bir kez daha</b> denenir
        /// (<see cref="MaxTotalAttempts"/>), ondan sonra deneme <b>durur</b> ama cevap
        /// yine <b>bilinmeyen</b> kalır. Yerleşmeyen bir yoklamayı ölçüm gibi kabul etmek,
        /// öldürülmüş bir denemeyi "bu kodlayıcı 10 bit taşıyamıyor" cümlesine çevirmek
        /// demekti; <see cref="Unsettled"/> bunun yerine durumu arayüze taşır.
        /// </summary>
        private bool Ready(string key, string codec, bool hdr10)
        {
            lock (_gate)
            {
                if (_answers.TryGetValue(key, out var answer))
                {
                    if (answer.Settled) return true;
                    if (answer.Attempts >= MaxTotalAttempts) return false;
                    var stuck = answer.Attempts >= MaxAttempts;
                    var cooling = stuck && Environment.TickCount64 - answer.LastAttemptTicks < RetryAfterFailureMs;
                    if (cooling) return false;
                }
                if (!_running.Add(key)) return false;
                _probes++;
            }

            Measure(key, codec, hdr10);
            return false;
        }

        /// <summary>
        /// Geçidin <b>girişi</b>. Ölçen çağrı üç değerli cevabı taşıyabiliyorsa oradan
        /// alınır; iki değerli <c>WorksAsEncoder</c>den geçirmek <c>Unmeasured</c>ı geçit
        /// daha <see cref="Answer"/>a yazmadan yok ediyordu ve "ölçemedik" ile "çalışmıyor"
        /// geçitten birebir aynı çıkıyordu. Üç değerli yüzü olmayan bir kaynak (ölçülerin
        /// sahteleri) iki değerli koldan geçer; o kolda üçüncü durum zaten yok.
        /// </summary>
        private EncoderProbeState ProbedEncoderState(string codec)
            => _source is IEncoderProbeState prober
                ? prober.WorksAsEncoderState(codec)
                : _source.WorksAsEncoder(codec) ? EncoderProbeState.Working : EncoderProbeState.NotWorking;

        private void Measure(string key, string codec, bool hdr10)
        {
            Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                var state = EncoderProbeState.Unmeasured;
                string? pixelFormat = null;
                Exception? failure = null;
                try
                {
                    if (hdr10)
                    {
                        pixelFormat = (_source as IHdr10EncoderAvailability)?.Hdr10PixelFormat(codec);
                        state = pixelFormat is null ? EncoderProbeState.NotWorking : EncoderProbeState.Working;
                    }
                    else state = ProbedEncoderState(codec);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                clock.Stop();

                lock (_gate)
                {
                    if (!_answers.TryGetValue(key, out var answer)) _answers[key] = answer = new Answer();
                    answer.Attempts++;
                    answer.LastAttemptTicks = Environment.TickCount64;
                    answer.Failure = failure?.Message;
                    answer.ElapsedMs = clock.ElapsedMilliseconds;
                    if (failure is null)
                    {
                        answer.State = state;
                        answer.Works = state == EncoderProbeState.Working;
                        answer.PixelFormat = pixelFormat;
                        answer.Settled = state != EncoderProbeState.Unmeasured
                                         && clock.ElapsedMilliseconds < UnsettledProbeMs;
                    }
                    else
                    {
                        answer.State = EncoderProbeState.Unmeasured;
                        answer.Works = false;
                        answer.PixelFormat = null;
                        answer.Settled = false;
                    }
                    _running.Remove(key);
                }

                _onMeasured();
            });
        }
    }
    /// <summary>
    /// Psy/AQ seçenek yoklamasını arka planda bir kez tüketir. <c>SupportsEncoderOption</c>
    /// ilk çağrısında ffmpeg süreci doğuruyor ve sonucu önbelleğe alıyor; o ilk çağrı
    /// arayüz iş parçacığına düşerse plan görünümü kodlayıcı başına yoklamanın süresi kadar
    /// kilitleniyor. Burada koşturulunca sonraki bütün okumalar önbellekten geliyor.
    /// </summary>
    internal static void WarmPsychovisualProbe(IEncoderAvailability capabilities)
    {
        foreach (var codec in FfmpegArguments.KnownCodecs)
            FfmpegArguments.PsychovisualArgs(codec, capabilities);
    }

    /// <summary>
    /// Yoklamanın "bu makinede donanım kodlayıcı var" cevabı. Plandaki kodlayıcı adı tek
    /// başına yetmez: <see cref="PlanCalculator"/> ölçülmemiş bir adayı geçici cevap olarak
    /// da döndürebiliyor ve o cevap ölçülmüş bir evet gibi okunursa sürücüsüz makine
    /// hızlı kipi açık görüyor. Geçici cevap bu yüzden aynı gövdenin çalıştırdığı gerçek
    /// yoklamayla doğrulanır; ölçülmüş bir seçim yeniden sınanmaz.
    /// </summary>
    internal static bool HardwareAvailableFrom(EncodePlan plan, EncoderProbeResult probe)
        => CodecModel.IsHardware(plan.Codec)
           && (!plan.CodecNotMeasured || (probe.Measured && probe.Succeeded));

    /// <summary>Ölçü için: yoklamanın arayüze taşıdığı donanım cevabı.</summary>
    internal bool HardwareEncoderAvailable => _hardwareEncoderAvailable;

    private async Task ProbeHardwareEncodersAsync()
    {
        var available = false;
        IEncoderAvailability? encoders = null;
        var verdict = HardwareVerdict.NotProbed;

        try
        {
            (encoders, available, verdict) = await Task.Run(() =>
            {
                var capabilities = EncoderCapabilities.Instance;
                WarmPsychovisualProbe(capabilities);
                var options = new PlanOptions { TargetMb = WhatsAppTargetMb, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast };
                var plan = PlanCalculator.Build(HardwareProbeSource, options, capabilities);
                var probe = capabilities.Probe(plan.Codec);
                var decision = HardwareVerdict.Decide(probe, plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);
                return ((IEncoderAvailability?)capabilities, HardwareAvailableFrom(plan, probe), decision);
            });
        }
        catch (Exception ex)
        {
            encoders = null;
            available = false;
            verdict = HardwareVerdict.NotProbed;
            TxtSystemStatus.Text = $"{Say("main.error.probe")}: {ex.Message}";
        }

        ApplyHardwareVerdict(encoders, available, verdict);
    }

    /// <summary>
    /// Yoklamanın sonucunu arayüze ve ayara bağlar. Yoklamadan ayrı durur ki açılış yolu
    /// ffmpeg çağrılmadan da sınanabilsin.
    /// </summary>
    internal void ApplyHardwareVerdict(IEncoderAvailability? encoders, bool available, HardwareVerdict verdict)
    {
        _encoders = encoders;
        _planEncoders = encoders is null
            ? null
            : new DeferredEncoderAvailability(encoders, () => Dispatcher.UIThread.Post(ScheduleRecalculate));
        if (_preview is not null) _preview.Availability = encoders;
        _hardwareProbed = true;
        _hardwareEncoderAvailable = available;
        _hardwareVerdict = verdict;

        var wasSyncing = _syncing;
        _syncing = true;
        ChkFastGpu.IsEnabled = available;
        ChkFastGpu.IsChecked = available && ResolveFastGpuSetting(verdict);
        _syncing = wasSyncing;

        ApplyFastGpuTip();
        Recalculate();
    }

    /// <summary>
    /// Kararı ayar dosyasıyla buluşturur. Dosyada değer varsa yoklama onu ezmez; yoksa
    /// bu açılışta bir kez yazılır ve bir daha yoklamaya sorulmaz.
    /// </summary>
    private bool ResolveFastGpuSetting(HardwareVerdict verdict)
    {
        try
        {
            var settings = UpdateSettings.Load(SettingsPathOverride);
            if (HardwareVerdict.ReprobeRequested()) settings.FastGpu = null;
            if (verdict.ApplyTo(settings)) settings.Save(SettingsPathOverride);
            return settings.FastGpu == true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
            return false;
        }
    }

}
