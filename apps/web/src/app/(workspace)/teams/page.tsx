"use client";

import { useSessionStore } from "@/features/session/sessionStore";
import { MyTeamsView } from "@/features/teams/MyTeamsView";

export default function MyTeamsPage() {
  const { user, language, token } = useSessionStore();
  if (!user) return null;
  return <MyTeamsView activeUser={user} language={language} token={token} />;
}
