"use client";

import { TeamManagementView } from "@/features/teams/TeamManagementView";
import { useSessionStore } from "@/features/session/sessionStore";

export default function ManagementTeamsPage() {
  const { user, language, token } = useSessionStore();
  if (!user) return null;
  return <TeamManagementView activeUser={user} language={language} token={token} />;
}
