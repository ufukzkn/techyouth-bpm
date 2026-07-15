import type { CSSProperties } from "react";

export type SegmentedOption<T extends string> = {
  value: T;
  label: string;
};

export function SlidingSegmentedControl<T extends string>({
  ariaLabel,
  name,
  onChange,
  options,
  value,
}: {
  ariaLabel: string;
  name: string;
  onChange: (value: T) => void;
  options: readonly SegmentedOption<T>[];
  value: T;
}) {
  const activeIndex = Math.max(0, options.findIndex((option) => option.value === value));

  return (
    <div
      aria-label={ariaLabel}
      className="sliding-segmented-control"
      role="radiogroup"
      style={
        {
          "--segment-count": options.length,
          "--segment-active-index": activeIndex,
        } as CSSProperties
      }
    >
      {options.map((option) => {
        const inputId = `${name}-${option.value}`;
        return (
          <div className="sliding-segmented-item" key={option.value}>
            <input
              checked={value === option.value}
              id={inputId}
              name={name}
              onChange={() => onChange(option.value)}
              type="radio"
              value={option.value}
            />
            <label className="sliding-segmented-option" htmlFor={inputId}>
              {option.label}
            </label>
          </div>
        );
      })}
      <div aria-hidden="true" className="sliding-segmented-slider" />
    </div>
  );
}
