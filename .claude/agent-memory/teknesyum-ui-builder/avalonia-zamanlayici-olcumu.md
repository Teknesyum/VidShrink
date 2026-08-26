---
name: avalonia-zamanlayici-olcumu
description: Test iş parçacığında DispatcherTimer tik atmaz; gecikmeli davranış karar kapısı + çağrılan yol olarak ölçülür
metadata:
  type: feedback
---

Gecikmeli arayüz davranışını (kaybolma, iniş, gecikmeli gizleme) testte duvar saatiyle
bekleyerek doğrulama. `AppHost` iş parçacığında ileti döngüsü yok, `DispatcherTimer`
hiç ateşlenmez; `Dispatcher.UIThread.RunJobs()` yalnız `Post` edilmiş işleri koşturur,
zamanlayıcıyı koşturmaz.

**Why:** T44/K2'de iki saniyelik iniş sayacı ölçülecekti; zamanlayıcıyı beklemek sonsuza
kadar yeşil-yalan verirdi.

**How to apply:** zamanlayıcıyı iki parçaya ayır ve ikisini ayrı ölç — (1) tikin sorduğu
karar kapısı (`HoverZone.ShouldHide(kuşak)` gibi bir iç metot), (2) zaman aşımının gittiği
yol (`ComparisonPanel.Descend()`). Süre değerinin temadan geldiğini ayrıca belirteci
okuyarak sına. Rapora "2 saniye beklendi" yazma; "zamanlayıcının çağırdığı yol koşturuldu"
yaz. Ayrıca `Dispatcher.UIThread.Post(..., DispatcherPriority.Render)` ile ertelenen
yerleşim işleri `RunJobs()` ile elle koşturulmalı.
