// Who may open the Settings tab.
//
// The server decides for real (the settings endpoints carry the Settings section policy, so a user without it gets a 403); the client
// only has to know whether to OFFER the tab and the button that leads there. It asks the backoffice's current user context, whose
// `allowedSections` lists the sections the signed-in user may open: the same fact the server's policy checks.

/** The alias of the backoffice's Settings section (UMB_SETTINGS_SECTION_ALIAS). */
export const SETTINGS_SECTION = 'Umb.Section.Settings';

/**
 * Whether a user with these sections may use the Settings tab: true or false once the current user has loaded, and undefined until
 * then (the context answers undefined while it loads). The caller shows nothing that depends on it while it is undefined.
 */
export function hasSettingsAccess(allowedSections: readonly string[] | undefined): boolean | undefined {
  return allowedSections === undefined ? undefined : allowedSections.includes(SETTINGS_SECTION);
}
