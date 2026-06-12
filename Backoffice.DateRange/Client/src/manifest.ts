export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "propertyEditorUi",
    alias: "Backoffice.DateRange.DateRangePicker",
    name: "Date Range",
    element: () => import("./date-range.element.js"),
    elementName: "bo-date-range",
    meta: {
      label: "Date Range",
      icon: "icon-calendar",
      group: "pickers",
      // Built-in JSON storage schema — stores the { from, to } object directly.
      propertyEditorSchemaAlias: "Umbraco.Plain.Json",
      settings: {
        properties: [
          {
            alias: "includeTime",
            label: "Include time",
            description: "Also capture a time for each end of the range.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle",
          },
          {
            alias: "minDate",
            label: "Earliest selectable date",
            description: "Optional. Dates before this cannot be chosen.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.DatePicker",
          },
          {
            alias: "maxDate",
            label: "Latest selectable date",
            description: "Optional. Dates after this cannot be chosen.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.DatePicker",
          },
        ],
        defaultData: [{ alias: "includeTime", value: false }],
      },
    },
  },
];
