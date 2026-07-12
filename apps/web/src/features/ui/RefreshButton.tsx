import { RefreshCw } from "lucide-react";
import { Button } from "@/features/ui/Button";

type RefreshButtonProps = {
  isRefreshing: boolean;
  label: string;
  refreshingLabel: string;
  onRefresh: () => void;
};

export function RefreshButton({ isRefreshing, label, refreshingLabel, onRefresh }: RefreshButtonProps) {
  return (
    <Button
      className="refresh-button"
      disabled={isRefreshing}
      leadingIcon={<RefreshCw className={isRefreshing ? "spin-icon" : undefined} size={16} aria-hidden="true" />}
      onClick={onRefresh}
      size="sm"
      variant="secondary"
    >
      {isRefreshing ? refreshingLabel : label}
    </Button>
  );
}
