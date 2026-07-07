import { translate, type TranslationKey } from "@/features/i18n/translations";
import { ApiError } from "@/lib/api";
import type { Language } from "@/lib/types";

const apiErrorKeys: Record<string, TranslationKey> = {
  "A valid email is required.": "api.error.emailRequired",
  "A valid session token is required.": "api.error.sessionRequired",
  "Account is not active.": "api.error.accountInactive",
  "Account is temporarily locked after multiple failed login attempts.": "api.error.accountLocked",
  "Account is waiting for admin approval.": "api.error.pendingApproval",
  "Current password is incorrect.": "api.error.currentPasswordIncorrect",
  "Display name is required.": "api.error.displayNameRequired",
  "Email is already registered.": "api.error.emailExists",
  "Email is already verified.": "api.error.emailAlreadyVerified",
  "Email recipient is not allowed for SMTP delivery.": "api.error.emailRecipientNotAllowed",
  "Admin users cannot delete their own account.": "api.error.selfDeleteNotAllowed",
  "Only Admin users can create users.": "api.error.adminCreateOnly",
  "Only Admin users can delete users.": "api.error.adminDeleteOnly",
  "Only Admin users can update access.": "api.error.adminAccessOnly",
  "Only Admin users can view user sessions.": "api.error.adminSessionsOnly",
  "Only Admin users can revoke user sessions.": "api.error.adminRevokeOnly",
  "Password must be at least 8 characters.": "api.error.passwordLength",
  "Session not found.": "api.error.sessionNotFound",
  "Temporary password email could not be sent.": "api.error.temporaryPasswordEmailFailed",
  "User not found.": "api.error.userNotFound",
  "User has workflow history and cannot be deleted.": "api.error.userDeleteHasHistory",
  "Username is required.": "api.error.usernameRequired",
  "Username or email is already registered.": "api.error.usernameEmailExists",
  "Username or password is incorrect.": "api.error.invalidLogin",
  "Verification code expired.": "api.error.verificationExpired",
  "Verification code is incorrect.": "api.error.verificationIncorrect",
  "Verification code was sent recently. Please wait before requesting another code.": "api.error.verificationCooldown",
};

export function localizeApiError(error: unknown, language: Language, fallback: string) {
  if (!(error instanceof ApiError)) {
    return fallback;
  }

  return error.errors.map((message) => translate(language, apiErrorKeys[message] ?? "api.error.generic")).join(" ");
}
