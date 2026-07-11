"use client";

import { ManagementCommunitiesView } from "@/features/app-shell/views/ManagementCommunitiesView";
import { useSessionStore } from "@/features/session/sessionStore";

export default function ManagementCommunitiesPage() {
  const { user, language, token } = useSessionStore();

  if (!user) return null;

  return <ManagementCommunitiesView activeUser={user} language={language} token={token} />;
}
