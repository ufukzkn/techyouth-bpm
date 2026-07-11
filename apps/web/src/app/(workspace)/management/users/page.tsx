"use client";

import { UsersAndRolesView } from "@/features/app-shell/views/UsersAndRolesView";
import { useSessionStore } from "@/features/session/sessionStore";

export default function ManagementUsersPage() {
  const { user, language, token } = useSessionStore();

  if (!user) return null;

  return <UsersAndRolesView activeUser={user} language={language} token={token} />;
}
