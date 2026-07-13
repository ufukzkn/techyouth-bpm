"use client";

import { InboxView } from "@/features/notifications/InboxView";
import { useSessionStore } from "@/features/session/sessionStore";

export default function InboxPage() {
  const { language, token, user } = useSessionStore();
  if (!user) return null;
  return <InboxView language={language} token={token} userId={user.id} />;
}
