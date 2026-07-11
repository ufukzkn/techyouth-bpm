"use client";

import { SystemLogsView } from "@/features/app-shell/views/SystemLogsView";
import { useSessionStore } from "@/features/session/sessionStore";

export default function LogsPage() {
  const { language, token } = useSessionStore();
  return <SystemLogsView language={language} token={token} />;
}
