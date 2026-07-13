import type { ButtonHTMLAttributes, ReactNode } from "react";

type IconButtonProps = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "aria-label" | "title"> & {
  children: ReactNode;
  label: string;
  tone?: "default" | "danger";
};

export function IconButton({ children, className = "", label, tone = "default", type = "button", ...props }: IconButtonProps) {
  return (
    <button
      aria-label={label}
      className={`icon-button ui-icon-button ui-icon-button-${tone} ${className}`.trim()}
      title={label}
      type={type}
      {...props}
    >
      {children}
    </button>
  );
}
