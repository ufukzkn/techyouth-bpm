import type {
  CreateFormRequest,
  CreateFormVersionRequest,
  FormDefinition,
  FormDefinitionVersion,
  FormFieldDefinition,
} from "@/lib/types";

export type FormVersionStatus = "draft" | "published" | "archived";

export type FormPageLayout = {
  /** Stable page key used by the client and version API, not a database row id. */
  id: string;
  title: string;
  description: string;
  fieldKeys: string[];
};

export type VersionedFormLayout = {
  versionId?: string;
  version: number;
  status: FormVersionStatus;
  pages: FormPageLayout[];
};

export type ResolvedFormPage = Omit<FormPageLayout, "fieldKeys"> & {
  fields: FormFieldDefinition[];
};

export type FormVersionPersistenceInput = {
  form: FormDefinition;
  request: CreateFormRequest;
  layout: VersionedFormLayout;
};

export type ResolvedFormVersion = {
  layout: VersionedFormLayout;
  fields: FormFieldDefinition[];
};

export type FormVersionAdapter = {
  resolveVersion?: (form: FormDefinition) => ResolvedFormVersion | null | undefined;
  resolveLayout?: (form: FormDefinition) => VersionedFormLayout | null | undefined;
  saveDraft?: (input: FormVersionPersistenceInput) => Promise<VersionedFormLayout | void>;
  publish?: (input: FormVersionPersistenceInput) => Promise<VersionedFormLayout | void>;
  archive?: (input: FormVersionPersistenceInput) => Promise<VersionedFormLayout | void>;
};

export function createLegacyFormLayout(form: FormDefinition, pageLabel: string): VersionedFormLayout {
  return {
    version: 1,
    status: "published",
    pages: [
      {
        id: "page-1",
        title: createPageTitle(pageLabel, 0),
        description: "",
        fieldKeys: sortFields(form.fields).map((field) => field.key),
      },
    ],
  };
}

export function normalizeFormLayout(
  form: FormDefinition,
  layout: VersionedFormLayout | null | undefined,
  pageLabel: string,
): VersionedFormLayout {
  if (!layout || layout.pages.length === 0) {
    return createLegacyFormLayout(form, pageLabel);
  }

  const sortedFields = sortFields(form.fields);
  const availableFieldKeys = new Set(sortedFields.map((field) => field.key));
  const assignedFieldKeys = new Set<string>();
  const usedPageIds = new Set<string>();

  const pages = layout.pages.map((page, index) => {
    const requestedId = page.id.trim() || `page-${index + 1}`;
    const id = createUniquePageId(requestedId, usedPageIds);
    usedPageIds.add(id);

    const fieldKeys = page.fieldKeys.filter((fieldKey) => {
      if (!availableFieldKeys.has(fieldKey) || assignedFieldKeys.has(fieldKey)) {
        return false;
      }

      assignedFieldKeys.add(fieldKey);
      return true;
    });

    return {
      id,
      title: page.title.trim() || createPageTitle(pageLabel, index),
      description: page.description?.trim() ?? "",
      fieldKeys,
    };
  });

  const unassignedFieldKeys = sortedFields
    .map((field) => field.key)
    .filter((fieldKey) => !assignedFieldKeys.has(fieldKey));

  if (unassignedFieldKeys.length > 0) {
    pages[0] = {
      ...pages[0],
      fieldKeys: [...pages[0].fieldKeys, ...unassignedFieldKeys],
    };
  }

  return {
    ...(layout.versionId ? { versionId: layout.versionId } : {}),
    version: normalizeVersion(layout.version),
    status: layout.status === "draft" || layout.status === "archived" ? layout.status : "published",
    pages,
  };
}

export function resolveFormPages(
  form: FormDefinition,
  layout: VersionedFormLayout | null | undefined,
  pageLabel: string,
): { layout: VersionedFormLayout; pages: ResolvedFormPage[] } {
  const normalizedLayout = normalizeFormLayout(form, layout, pageLabel);
  const fieldsByKey = new Map<string, FormFieldDefinition>();

  for (const field of sortFields(form.fields)) {
    if (!fieldsByKey.has(field.key)) {
      fieldsByKey.set(field.key, field);
    }
  }

  return {
    layout: normalizedLayout,
    pages: normalizedLayout.pages.map((page) => ({
      id: page.id,
      title: page.title,
      description: page.description,
      fields: page.fieldKeys.flatMap((fieldKey) => {
        const field = fieldsByKey.get(fieldKey);
        return field ? [field] : [];
      }),
    })),
  };
}

export function fromFormDefinitionVersion(version: FormDefinitionVersion): ResolvedFormVersion {
  const pages = version.pages.slice().sort((first, second) => first.sortOrder - second.sortOrder);
  let globalSortOrder = 0;
  const fields = pages.flatMap((page) =>
    sortFields(page.fields).map((field) => ({ ...field, sortOrder: (globalSortOrder += 1) })),
  );

  return {
    fields,
    layout: {
      versionId: version.id,
      version: version.versionNumber,
      status:
        version.status === "Draft" ? "draft" : version.status === "Archived" ? "archived" : "published",
      pages: pages.map((page) => ({
        id: page.key,
        title: page.title,
        description: page.description,
        fieldKeys: sortFields(page.fields).map((field) => field.key),
      })),
    },
  };
}

export function toCreateFormVersionRequest(input: FormVersionPersistenceInput): CreateFormVersionRequest {
  const fieldsByKey = new Map(input.request.fields.map((field) => [field.key, field]));

  return {
    pages: input.layout.pages.map((page, pageIndex) => ({
      key: page.id,
      title: page.title,
      description: page.description,
      sortOrder: pageIndex,
      fields: page.fieldKeys.flatMap((fieldKey, fieldIndex) => {
        const field = fieldsByKey.get(fieldKey);
        return field ? [{ ...field, sortOrder: fieldIndex }] : [];
      }),
    })),
  };
}

function createPageTitle(pageLabel: string, index: number) {
  return `${pageLabel} ${index + 1}`;
}

function createUniquePageId(requestedId: string, usedPageIds: Set<string>) {
  if (!usedPageIds.has(requestedId)) {
    return requestedId;
  }

  let suffix = 2;
  while (usedPageIds.has(`${requestedId}-${suffix}`)) {
    suffix += 1;
  }

  return `${requestedId}-${suffix}`;
}

function normalizeVersion(version: number) {
  return Number.isInteger(version) && version > 0 ? version : 1;
}

function sortFields(fields: FormFieldDefinition[]) {
  return fields.slice().sort((first, second) => first.sortOrder - second.sortOrder);
}
