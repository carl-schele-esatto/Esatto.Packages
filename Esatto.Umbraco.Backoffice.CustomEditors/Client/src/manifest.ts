export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "propertyEditorUi",
    alias: "Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox",
    name: "Encrypted Textbox",
    element: () => import("./encrypted-textbox.element.js"),
    meta: {
      label: "Encrypted Textbox",
      icon: "icon-lock",
      group: "common",
      // Custom C# schema (EncryptedTextboxDataEditor) — encrypts at rest.
      propertyEditorSchemaAlias: "Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox",
      settings: {
        properties: [
          {
            alias: "mask",
            label: "Mask value",
            description: "Hide the value on screen behind a reveal toggle.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle",
          },
        ],
        defaultData: [{ alias: "mask", value: true }],
      },
    },
  },
  {
    type: "propertyEditorUi",
    alias: "Esatto.Umbraco.Backoffice.CustomEditors.DateRange",
    name: "Date Range",
    element: () => import("./date-range.element.js"),
    meta: {
      label: "Date Range",
      icon: "icon-calendar",
      group: "pickers",
      propertyEditorSchemaAlias: "Umbraco.Plain.Json",
      settings: {
        properties: [
          { alias: "includeTime", label: "Include time", description: "Also capture a time for each end of the range.", propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle" },
          { alias: "minDate", label: "Earliest selectable date", description: "Optional. Dates before this cannot be chosen.", propertyEditorUiAlias: "Umb.PropertyEditorUi.DatePicker" },
          { alias: "maxDate", label: "Latest selectable date", description: "Optional. Dates after this cannot be chosen.", propertyEditorUiAlias: "Umb.PropertyEditorUi.DatePicker" }
        ],
        defaultData: [{ alias: "includeTime", value: false }]
      }
    }
  }
];
