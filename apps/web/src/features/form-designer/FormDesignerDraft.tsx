"use client";

import { ChevronDown, ChevronUp, Plus, Save, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";
import {
  createDefaultField,
  createDefaultOptions,
  fieldTypeLabels,
  fieldTypeUsesOptions,
  supportedFieldTypes,
} from "@/features/forms/fieldTypes";
import { useSessionStore } from "@/features/session/sessionStore";
import { api, ApiError } from "@/lib/api";
import type { CreateFormRequest, FieldType, FormFieldDefinition, ValidationRule } from "@/lib/types";

type DesignerField = Omit<FormFieldDefinition, "id"> & {
  id: string;
};

const initialFields: DesignerField[] = [
  {
    id: "customerName",
    key: "customerName",
    label: "Musteri adi",
    type: "Text",
    required: true,
    sortOrder: 1,
    options: [],
    validationRules: [],
  },
  {
    id: "requestType",
    key: "requestType",
    label: "Talep tipi",
    type: "Select",
    required: true,
    sortOrder: 2,
    options: ["Izin", "Masraf", "Satinalma"],
    validationRules: [],
  },
  {
    id: "approvalNote",
    key: "approvalNote",
    label: "Onay aciklamasi",
    type: "Text",
    required: false,
    sortOrder: 3,
    options: [],
    validationRules: [
      {
        ruleType: "RequiredWhen",
        dependsOnFieldKey: "requestType",
        expectedValue: "Satinalma",
        message: "Satinalma taleplerinde onay aciklamasi zorunludur.",
      },
    ],
  },
];

export function FormDesignerDraft() {
  const token = useSessionStore((state) => state.token);
  const [fields, setFields] = useState<DesignerField[]>(initialFields);
  const [formName, setFormName] = useState("Demo Surec Formu");
  const [description, setDescription] = useState("Frontend tarafinda tasarlanan form modeli");
  const [label, setLabel] = useState("Masraf merkezi");
  const [type, setType] = useState<FieldType>("Text");
  const [required, setRequired] = useState(false);
  const [saveState, setSaveState] = useState<"idle" | "saving" | "success" | "error">("idle");
  const [message, setMessage] = useState("Form henuz kaydedilmedi.");
  const fieldErrors = useMemo(() => validateDesignerFields(fields), [fields]);
  const hasFieldErrors = Object.keys(fieldErrors).length > 0;

  const formModel = useMemo<CreateFormRequest>(
    () => ({
      name: formName,
      description,
      fields: fields.map((field, index) => ({
        key: field.key.trim(),
        label: field.label.trim(),
        type: field.type,
        required: field.required,
        sortOrder: index + 1,
        options: fieldTypeUsesOptions(field.type) ? field.options.map((option) => option.trim()).filter(Boolean) : [],
        validationRules: field.validationRules.map((rule) => ({
          ruleType: rule.ruleType,
          dependsOnFieldKey: rule.dependsOnFieldKey.trim(),
          expectedValue: rule.expectedValue.trim(),
          message: rule.message.trim(),
        })),
      })),
    }),
    [description, fields, formName],
  );

  function addField() {
    const field = createDefaultField({
      label,
      type,
      required,
      sortOrder: fields.length + 1,
    });
    const nextField: DesignerField = {
      ...field,
      id: `${field.key}-${Date.now()}`,
    };

    setFields((current) => [...current, nextField]);
    setLabel("");
    setType("Text");
    setRequired(false);
    setSaveState("idle");
    setMessage("Formda kaydedilmemis degisiklikler var.");
  }

  function updateField(id: string, patch: Partial<Omit<DesignerField, "id">>) {
    setFields((current) => current.map((field) => (field.id === id ? { ...field, ...patch } : field)));
    markUnsaved();
  }

  function updateFieldType(id: string, nextType: FieldType) {
    setFields((current) =>
      current.map((field) => {
        if (field.id !== id) {
          return field;
        }

        return {
          ...field,
          type: nextType,
          options: fieldTypeUsesOptions(nextType)
            ? field.options.length > 0
              ? field.options
              : createDefaultOptions(nextType)
            : [],
        };
      }),
    );
    markUnsaved();
  }

  function removeField(id: string) {
    setFields((current) => normalizeSortOrder(current.filter((field) => field.id !== id)));
    markUnsaved();
  }

  function toggleRequired(id: string) {
    setFields((current) =>
      current.map((field) => (field.id === id ? { ...field, required: !field.required } : field)),
    );
    markUnsaved();
  }

  function moveField(id: string, direction: -1 | 1) {
    setFields((current) => {
      const currentIndex = current.findIndex((field) => field.id === id);
      const nextIndex = currentIndex + direction;

      if (currentIndex < 0 || nextIndex < 0 || nextIndex >= current.length) {
        return current;
      }

      const nextFields = [...current];
      const [field] = nextFields.splice(currentIndex, 1);
      nextFields.splice(nextIndex, 0, field);
      return normalizeSortOrder(nextFields);
    });
    markUnsaved();
  }

  function addOption(fieldId: string) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId ? { ...field, options: [...field.options, `Secenek ${field.options.length + 1}`] } : field,
      ),
    );
    markUnsaved();
  }

  function updateOption(fieldId: string, optionIndex: number, value: string) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId
          ? {
              ...field,
              options: field.options.map((option, index) => (index === optionIndex ? value : option)),
            }
          : field,
      ),
    );
    markUnsaved();
  }

  function removeOption(fieldId: string, optionIndex: number) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId
          ? {
              ...field,
              options: field.options.filter((_, index) => index !== optionIndex),
            }
          : field,
      ),
    );
    markUnsaved();
  }

  function addRequiredWhenRule(fieldId: string) {
    setFields((current) =>
      current.map((field) => {
        if (field.id !== fieldId) {
          return field;
        }

        const dependency = findFirstDependencyField(current, field);
        const rule: ValidationRule = {
          ruleType: "RequiredWhen",
          dependsOnFieldKey: dependency?.key.trim() ?? "",
          expectedValue: dependency ? getDefaultExpectedValue(dependency) : "",
          message: `${field.label || field.key} bu kosulda zorunludur.`,
        };

        return { ...field, validationRules: [...field.validationRules, rule] };
      }),
    );
    markUnsaved();
  }

  function updateRequiredWhenRule(fieldId: string, ruleIndex: number, patch: Partial<ValidationRule>) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId
          ? {
              ...field,
              validationRules: field.validationRules.map((rule, index) =>
                index === ruleIndex ? { ...rule, ...patch } : rule,
              ),
            }
          : field,
      ),
    );
    markUnsaved();
  }

  function updateRuleDependency(fieldId: string, ruleIndex: number, dependsOnFieldKey: string) {
    setFields((current) =>
      current.map((field) => {
        if (field.id !== fieldId) {
          return field;
        }

        const dependency = current.find((candidate) => candidate.key.trim() === dependsOnFieldKey);
        return {
          ...field,
          validationRules: field.validationRules.map((rule, index) =>
            index === ruleIndex
              ? {
                  ...rule,
                  dependsOnFieldKey,
                  expectedValue: dependency ? getDefaultExpectedValue(dependency) : "",
                }
              : rule,
          ),
        };
      }),
    );
    markUnsaved();
  }

  function removeRequiredWhenRule(fieldId: string, ruleIndex: number) {
    setFields((current) =>
      current.map((field) =>
        field.id === fieldId
          ? {
              ...field,
              validationRules: field.validationRules.filter((_, index) => index !== ruleIndex),
            }
          : field,
      ),
    );
    markUnsaved();
  }

  function markUnsaved() {
    setSaveState("idle");
    setMessage("Formda kaydedilmemis degisiklikler var.");
  }

  async function saveForm() {
    if (!token) {
      setSaveState("error");
      setMessage("Form kaydetmek icin API oturumu gerekli.");
      return;
    }

    if (hasFieldErrors) {
      setSaveState("error");
      setMessage("Form kaydedilmeden once alan hatalari duzeltilmeli.");
      return;
    }

    try {
      setSaveState("saving");
      const saved = await api.createForm(token, formModel);
      setSaveState("success");
      setMessage(`Form SQLite veritabanina kaydedildi: ${saved.name}`);
    } catch (error) {
      setSaveState("error");
      setMessage(error instanceof ApiError ? error.errors.join(" ") : "Form kaydedilemedi.");
    }
  }

  return (
    <section className="designer-section">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Form Tasarimi</span>
          <h2>Dinamik form modeli</h2>
        </div>
        <p>Alanlar UI tarafinda tasarlanir ve backend tarafinda form definition olarak saklanir.</p>
      </div>

      <div className="designer-grid">
        <div className="tool-panel">
          <h3>Form bilgisi</h3>
          <label>
            Form adi
            <input value={formName} onChange={(event) => setFormName(event.target.value)} />
          </label>
          <label>
            Aciklama
            <input value={description} onChange={(event) => setDescription(event.target.value)} />
          </label>
          <button className="primary-button" disabled={saveState === "saving"} type="button" onClick={saveForm}>
            <Save size={18} />
            {saveState === "saving" ? "Kaydediliyor" : "Formu kaydet"}
          </button>
          {hasFieldErrors ? <p className="field-error">Alanlarda kaydetmeyi engelleyen hatalar var.</p> : null}
          <p className={`status-line status-line-${saveState}`}>{message}</p>
        </div>

        <div className="tool-panel">
          <h3>Alan ekle</h3>
          <label>
            Etiket
            <input value={label} onChange={(event) => setLabel(event.target.value)} placeholder="Alan etiketi" />
          </label>
          <label>
            Tip
            <select value={type} onChange={(event) => setType(event.target.value as FieldType)}>
              {supportedFieldTypes.map((fieldType) => (
                <option key={fieldType} value={fieldType}>
                  {fieldTypeLabels[fieldType]}
                </option>
              ))}
            </select>
          </label>
          <label className="checkbox-row">
            <input checked={required} onChange={(event) => setRequired(event.target.checked)} type="checkbox" />
            Zorunlu alan
          </label>
          <button className="secondary-button" type="button" onClick={addField}>
            <Plus size={18} />
            Alan ekle
          </button>
        </div>

        <div className="field-list" aria-label="Designed fields">
          {fields.map((field, index) => (
            <article className="field-card field-editor" key={field.id}>
              <div className="field-editor-header">
                <div>
                  <strong>{field.label || "Etiketsiz alan"}</strong>
                  <span>
                    {field.key || "key yok"} - {field.type} - Sira {field.sortOrder}
                  </span>
                </div>
                <div className="field-editor-actions">
                  <button
                    className="icon-button"
                    disabled={index === 0}
                    onClick={() => moveField(field.id, -1)}
                    type="button"
                    aria-label={`${field.label || field.key} yukari tasi`}
                  >
                    <ChevronUp size={17} />
                  </button>
                  <button
                    className="icon-button"
                    disabled={index === fields.length - 1}
                    onClick={() => moveField(field.id, 1)}
                    type="button"
                    aria-label={`${field.label || field.key} asagi tasi`}
                  >
                    <ChevronDown size={17} />
                  </button>
                  <button
                    className="icon-button"
                    onClick={() => removeField(field.id)}
                    type="button"
                    aria-label={`${field.label || field.key} sil`}
                  >
                    <Trash2 size={17} />
                  </button>
                </div>
              </div>

              <div className="field-editor-grid">
                <label>
                  Key
                  <input value={field.key} onChange={(event) => updateField(field.id, { key: event.target.value })} />
                  {fieldErrors[field.id]?.key ? <span className="field-error">{fieldErrors[field.id]?.key}</span> : null}
                </label>
                <label>
                  Etiket
                  <input value={field.label} onChange={(event) => updateField(field.id, { label: event.target.value })} />
                  {fieldErrors[field.id]?.label ? (
                    <span className="field-error">{fieldErrors[field.id]?.label}</span>
                  ) : null}
                </label>
                <label>
                  Tip
                  <select value={field.type} onChange={(event) => updateFieldType(field.id, event.target.value as FieldType)}>
                    {supportedFieldTypes.map((fieldType) => (
                      <option key={fieldType} value={fieldType}>
                        {fieldTypeLabels[fieldType]}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="checkbox-row field-required-toggle">
                  <input checked={field.required} onChange={() => toggleRequired(field.id)} type="checkbox" />
                  Zorunlu
                </label>
              </div>

              {fieldTypeUsesOptions(field.type) ? (
                <div className="option-editor">
                  <div className="option-editor-header">
                    <strong>Options</strong>
                    <button className="secondary-button" type="button" onClick={() => addOption(field.id)}>
                      <Plus size={16} />
                      Option ekle
                    </button>
                  </div>
                  {fieldErrors[field.id]?.options ? (
                    <span className="field-error">{fieldErrors[field.id]?.options}</span>
                  ) : null}
                  <div className="option-list">
                    {field.options.map((option, optionIndex) => (
                      <div className="option-row" key={`${field.id}-${optionIndex}`}>
                        <label>
                          Label / value
                          <input value={option} onChange={(event) => updateOption(field.id, optionIndex, event.target.value)} />
                        </label>
                        <button
                          className="icon-button"
                          type="button"
                          onClick={() => removeOption(field.id, optionIndex)}
                          aria-label={`${option || "option"} sil`}
                        >
                          <Trash2 size={16} />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              ) : null}

              <div className="rule-editor">
                <div className="rule-editor-header">
                  <div>
                    <strong>Dependent validation</strong>
                    <span>RequiredWhen: baska bir alan belirli degeri alinca bu alan zorunlu olur.</span>
                  </div>
                  <button
                    className="secondary-button"
                    type="button"
                    disabled={getDependencyCandidates(fields, field).length === 0}
                    onClick={() => addRequiredWhenRule(field.id)}
                  >
                    <Plus size={16} />
                    Rule ekle
                  </button>
                </div>

                {field.validationRules.length === 0 ? (
                  <p className="empty-state">Bu alan icin dependent rule tanimli degil.</p>
                ) : null}

                <div className="rule-list">
                  {field.validationRules.map((rule, ruleIndex) => {
                    const dependency = fields.find((candidate) => candidate.key.trim() === rule.dependsOnFieldKey);
                    const candidates = getDependencyCandidates(fields, field);
                    const ruleError = fieldErrors[field.id]?.rules?.[ruleIndex];

                    return (
                      <div className="rule-row" key={`${field.id}-rule-${ruleIndex}`}>
                        <label>
                          Bagli alan
                          <select
                            value={rule.dependsOnFieldKey}
                            onChange={(event) => updateRuleDependency(field.id, ruleIndex, event.target.value)}
                          >
                            <option value="">Alan seciniz</option>
                            {candidates.map((candidate) => (
                              <option key={candidate.id} value={candidate.key.trim()}>
                                {candidate.label || candidate.key}
                              </option>
                            ))}
                          </select>
                        </label>

                        <ExpectedValueInput
                          dependency={dependency}
                          expectedValue={rule.expectedValue}
                          onChange={(expectedValue) => updateRequiredWhenRule(field.id, ruleIndex, { expectedValue })}
                        />

                        <label>
                          Mesaj
                          <input
                            value={rule.message}
                            onChange={(event) => updateRequiredWhenRule(field.id, ruleIndex, { message: event.target.value })}
                          />
                        </label>

                        <button
                          className="icon-button"
                          type="button"
                          onClick={() => removeRequiredWhenRule(field.id, ruleIndex)}
                          aria-label="Rule sil"
                        >
                          <Trash2 size={16} />
                        </button>

                        {ruleError ? <span className="field-error rule-error">{ruleError}</span> : null}
                      </div>
                    );
                  })}
                </div>
              </div>
            </article>
          ))}
        </div>

        <pre className="json-preview">{JSON.stringify(formModel, null, 2)}</pre>
      </div>
    </section>
  );
}

function normalizeSortOrder(fields: DesignerField[]) {
  return fields.map((field, index) => ({ ...field, sortOrder: index + 1 }));
}

type DesignerFieldErrors = Record<string, { key?: string; label?: string; options?: string; rules?: Record<number, string> }>;

function validateDesignerFields(fields: DesignerField[]) {
  const errors: DesignerFieldErrors = {};
  const keyCounts = fields.reduce<Record<string, number>>((current, field) => {
    const key = field.key.trim().toLowerCase();
    if (key) {
      current[key] = (current[key] ?? 0) + 1;
    }

    return current;
  }, {});

  for (const field of fields) {
    const fieldError: DesignerFieldErrors[string] = {};
    const key = field.key.trim();

    if (!key) {
      fieldError.key = "Field key zorunludur.";
    } else if (keyCounts[key.toLowerCase()] > 1) {
      fieldError.key = "Field key benzersiz olmalidir.";
    }

    if (!field.label.trim()) {
      fieldError.label = "Label zorunludur.";
    }

    if (fieldTypeUsesOptions(field.type)) {
      const filledOptions = field.options.map((option) => option.trim()).filter(Boolean);
      if (filledOptions.length === 0) {
        fieldError.options = "Select/checkbox alanlari icin en az bir option zorunludur.";
      }
    }

    for (const [ruleIndex, rule] of field.validationRules.entries()) {
      const dependency = fields.find((candidate) => candidate.key.trim() === rule.dependsOnFieldKey.trim());
      const ruleError = validateRequiredWhenRule(field, rule, dependency);
      if (ruleError) {
        fieldError.rules = { ...fieldError.rules, [ruleIndex]: ruleError };
      }
    }

    if (Object.keys(fieldError).length > 0) {
      errors[field.id] = fieldError;
    }
  }

  return errors;
}

function getDependencyCandidates(fields: DesignerField[], field: DesignerField) {
  return fields.filter((candidate) => candidate.id !== field.id && candidate.key.trim().length > 0);
}

function findFirstDependencyField(fields: DesignerField[], field: DesignerField) {
  return getDependencyCandidates(fields, field)[0];
}

function getDefaultExpectedValue(field: DesignerField) {
  if (field.type === "Checkbox") {
    return "true";
  }

  if (field.type === "Select") {
    return field.options.map((option) => option.trim()).find(Boolean) ?? "";
  }

  return "";
}

function validateRequiredWhenRule(field: DesignerField, rule: ValidationRule, dependency?: DesignerField) {
  if (rule.ruleType !== "RequiredWhen") {
    return undefined;
  }

  if (!rule.dependsOnFieldKey.trim()) {
    return "Bagli alan secilmelidir.";
  }

  if (!dependency) {
    return "Bagli alan mevcut alanlardan biri olmalidir.";
  }

  if (dependency.id === field.id || dependency.key.trim() === field.key.trim()) {
    return "Alan kendisine bagimli olamaz.";
  }

  if (!rule.expectedValue.trim()) {
    return "Beklenen deger bos birakilamaz.";
  }

  if (dependency.type === "Select") {
    const options = dependency.options.map((option) => option.trim()).filter(Boolean);
    if (!options.includes(rule.expectedValue.trim())) {
      return "Beklenen deger bagli select alaninin option degerlerinden biri olmalidir.";
    }
  }

  if (dependency.type === "Checkbox" && !["true", "false"].includes(rule.expectedValue.trim())) {
    return "Checkbox bagimli kurali icin beklenen deger true veya false olmalidir.";
  }

  return undefined;
}

function ExpectedValueInput({
  dependency,
  expectedValue,
  onChange,
}: {
  dependency?: DesignerField;
  expectedValue: string;
  onChange: (expectedValue: string) => void;
}) {
  if (dependency?.type === "Select") {
    const options = dependency.options.map((option) => option.trim()).filter(Boolean);

    return (
      <label>
        Beklenen deger
        <select value={expectedValue} onChange={(event) => onChange(event.target.value)}>
          <option value="">Deger seciniz</option>
          {options.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </label>
    );
  }

  if (dependency?.type === "Checkbox") {
    return (
      <label>
        Beklenen deger
        <select value={expectedValue} onChange={(event) => onChange(event.target.value)}>
          <option value="">Deger seciniz</option>
          <option value="true">true</option>
          <option value="false">false</option>
        </select>
      </label>
    );
  }

  return (
    <label>
      Beklenen deger
      <input value={expectedValue} onChange={(event) => onChange(event.target.value)} placeholder="Orn. Satinalma" />
    </label>
  );
}
