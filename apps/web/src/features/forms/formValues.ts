import type { FormDefinition } from "@/lib/types";

export type FormValue = string | number | boolean;
export type FormValues = Record<string, FormValue>;

export function buildInitialValues(form: FormDefinition) {
  return form.fields.reduce<FormValues>((current, field) => {
    current[field.key] = field.type === "Checkbox" ? false : "";
    return current;
  }, {});
}

export function prepareFormData(form: FormDefinition, values: FormValues): Record<string, unknown> {
  return form.fields.reduce<Record<string, unknown>>((current, field) => {
    const value = values[field.key];

    if (field.type === "Number") {
      current[field.key] = value === "" ? "" : typeof value === "number" ? value : Number(value);
      return current;
    }

    current[field.key] = value;
    return current;
  }, {});
}
