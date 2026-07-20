"use client";

import { useRouter } from "next/navigation";
import { useSessionStore } from "@/features/session/sessionStore";
import { ErrorScreen } from "@/features/ui/ErrorScreen";

export default function WorkspaceError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const router = useRouter();
  const language = useSessionStore((state) => state.language);
  const logout = useSessionStore((state) => state.logout);

  return (
    <ErrorScreen
      language={language}
      reference={error.digest}
      onRetry={reset}
      onDashboard={() => router.replace("/dashboard")}
      onLogin={() => {
        logout();
        router.replace("/login");
      }}
    />
  );
}
