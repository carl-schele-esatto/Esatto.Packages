import { describe, it, expect } from "vitest";
import { isContentSurface } from "./surface.logic.js";

describe("isContentSurface", () => {
  it("is true for the content section", () => {
    expect(
      isContentSurface("https://x/umbraco/section/content/workspace/document/edit/abc"),
    ).toBe(true);
  });

  it("is true for the bare content section path", () => {
    expect(isContentSurface("https://x/umbraco/section/content")).toBe(true);
    expect(isContentSurface("https://x/umbraco/section/content?foo=1")).toBe(true);
  });

  it("is false for the settings section", () => {
    expect(
      isContentSurface("https://x/umbraco/section/settings/workspace/document-type/edit/abc"),
    ).toBe(false);
  });

  it("is false for media and members", () => {
    expect(isContentSurface("https://x/umbraco/section/media/workspace")).toBe(false);
    expect(isContentSurface("https://x/umbraco/section/member-management/x")).toBe(false);
  });

  it("does not match a content-prefixed section like content-blueprints", () => {
    expect(isContentSurface("https://x/umbraco/section/content-blueprints/x")).toBe(false);
  });

  it("is false for an empty string", () => {
    expect(isContentSurface("")).toBe(false);
  });
});
