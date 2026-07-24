"use client";

import { SystemLogsView } from "@/features/app-shell/views/SystemLogsView";
import { useSessionStore } from "@/features/session/sessionStore";

export default function LogsPage() {
  const { language, token, user } = useSessionStore();
  if (!user) {
    return null;
  }
  return <SystemLogsView activeUser={user} language={language} token={token} />;
}
