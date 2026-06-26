type PrototypeLogoProps = {
  size?: number;
};

export function PrototypeLogo({ size = 34 }: PrototypeLogoProps) {
  return (
    <svg
      aria-hidden="true"
      className="prototype-logo"
      fill="none"
      height={size}
      viewBox="0 0 48 48"
      width={size}
      xmlns="http://www.w3.org/2000/svg"
    >
      <rect className="prototype-logo-bg" height="48" rx="12" width="48" />
      <path
        className="prototype-logo-flow"
        d="M14 16.5C18.8 10.8 29.5 10.7 34.7 16.1C38.2 19.7 37.8 25.2 34.1 28.5C30.1 32.1 22.1 32 18.6 28.3C16.2 25.8 17 22.5 19.8 20.8C22.6 19.1 27.5 19.3 30.3 21.4"
        strokeLinecap="round"
        strokeWidth="4"
      />
      <path className="prototype-logo-check" d="M15 34L20.2 38.5L33 29" strokeLinecap="round" strokeLinejoin="round" strokeWidth="3.4" />
    </svg>
  );
}
