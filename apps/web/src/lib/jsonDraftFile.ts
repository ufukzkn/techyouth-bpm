export const maxDraftFileSizeBytes = 1024 * 1024;

export async function readJsonDraftFile(file: File) {
  if (file.size > maxDraftFileSizeBytes) {
    throw new Error("DRAFT_FILE_TOO_LARGE");
  }

  return file.text();
}

export function downloadJsonDraft(fileName: string, content: string) {
  const blob = new Blob([content], { type: "application/json;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

export function toSafeFileName(value: string, fallback: string) {
  const normalized = value
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-zA-Z0-9_-]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();

  return normalized || fallback;
}
