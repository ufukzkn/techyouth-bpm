"use client";

import "./globals.css";
import { useSessionStore } from "@/features/session/sessionStore";
import { ErrorScreen } from "@/features/ui/ErrorScreen";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const language = useSessionStore((state) => state.language);
  const theme = useSessionStore((state) => state.theme);
  const logout = useSessionStore((state) => state.logout);

  return (
    <html lang={language} data-theme={theme}>
      <body>
        <ErrorScreen
          language={language}
          reference={error.digest}
          onRetry={reset}
          onDashboard={() => window.location.assign("/dashboard")}
          onLogin={() => {
            logout();
            window.location.assign("/login");
          }}
        />
      </body>
    </html>
  );
}
