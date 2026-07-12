import { LoaderCircle } from "lucide-react";
import type { ButtonHTMLAttributes, ReactNode } from "react";

export type ButtonVariant = "primary" | "secondary" | "success" | "danger";
export type ButtonSize = "sm" | "md";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  isLoading?: boolean;
  leadingIcon?: ReactNode;
  size?: ButtonSize;
  variant?: ButtonVariant;
};

const variantClasses: Record<ButtonVariant, string> = {
  primary: "primary-button",
  secondary: "secondary-button",
  success: "success-button",
  danger: "danger-button",
};

export function Button({
  children,
  className = "",
  disabled,
  isLoading = false,
  leadingIcon,
  size = "md",
  type = "button",
  variant = "primary",
  ...props
}: ButtonProps) {
  return (
    <button
      className={`ui-button ui-button-${size} ${variantClasses[variant]} ${className}`.trim()}
      disabled={disabled || isLoading}
      type={type}
      {...props}
    >
      {isLoading ? <LoaderCircle className="spin-icon" size={16} aria-hidden="true" /> : leadingIcon}
      <span>{children}</span>
    </button>
  );
}
