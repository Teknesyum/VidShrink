
import links from './links.json';

export type Translate = (key: string) => string;

type Props = { t: Translate };

const BASE =
  'relative inline-flex items-center justify-center min-h-[var(--tk-target-min)] min-w-[var(--tk-target-min)] no-underline select-none ' +
  'tracking-[var(--tk-tr-label)] rounded-[var(--tk-r)] border-0 bg-transparent px-[var(--tk-input-padding-x)] ' +
  'ease-[--tk-e-out] duration-[--tk-t-instant] transition-[transform] active:scale-[var(--tk-scale-press)] ' +
  "after:content-[''] after:absolute after:left-[var(--tk-input-padding-x)] after:right-[var(--tk-input-padding-x)] after:bottom-0 " +
  'after:h-[var(--tk-focus-w)] after:bg-current after:scale-x-0 after:origin-center ' +
  'after:transition-transform after:duration-[--tk-t-instant] after:ease-[--tk-e-out] hover:after:scale-x-100';

const SUPPORT = `${BASE} gap-1.5 text-[length:var(--tk-label-support-fs)] font-[var(--tk-label-support-fw)] text-[var(--tk-label-support-color)]`;

const BRAND = `${BASE} text-[length:var(--tk-label-brand-fs)] font-[var(--tk-label-brand-fw)] text-[var(--tk-label-brand-color)]`;

function CoffeeIcon() {
  return (
    <svg
      width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
      className="transition-transform duration-[--tk-t-instant] ease-[--tk-e-out] group-hover:scale-110"
    >
      <path d="M4 9h13v7a4 4 0 0 1-4 4H8a4 4 0 0 1-4-4V9Z" />
      <path d="M17 11h1.5a2.5 2.5 0 0 1 0 5H17" />
      <path d="M8 2.5v2M12 2.5v2" />
    </svg>
  );
}

function Support({ t }: Props) {
  if (!links.sponsorEnabled) return null;
  return (
    <a
      href={links.sponsor}
      target="_blank"
      rel="noopener noreferrer"
      title={t('sig.supportTitle')}
      className={`group tk-no-drag ${SUPPORT}`}
    >
      <CoffeeIcon />
      {t('sig.support')}
    </a>
  );
}

function Brand({ t }: Props) {
  return (
    <a
      href={links.github}
      target="_blank"
      rel="noopener noreferrer"
      title={t('sig.brandTitle')}
      className={`tk-no-drag ${BRAND}`}
    >
      {t('sig.brand')}
    </a>
  );
}

export function Signature({ t }: Props) {
  return (
    <div className="flex items-center gap-2">
      <Support t={t} />
      <Brand t={t} />
    </div>
  );
}

export function SignatureFooter({ t }: Props) {
  return (
    <div className="mt-6 pt-3 border-t border-[var(--tk-border-decorative)] flex items-center justify-end gap-2">
      <Support t={t} />
      <Brand t={t} />
    </div>
  );
}
