import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { Editor } from "@tiptap/core";
import { createEditor, toMarkdown, setMarkdown } from "../src/editor";

/**
 * Round-trip fidelity: markdown → TipTap doc → markdown. Ports the WPF app's cases.
 * These are the gate on the markdown layer working at all (tiptap-markdown storage)
 * and on the output matching our vocabulary.
 */
describe("markdown round-trip", () => {
  let el: HTMLElement;
  let editor: Editor;

  beforeEach(() => {
    el = document.createElement("div");
    document.body.appendChild(el);
    editor = createEditor(el, () => {});
  });

  afterEach(() => {
    editor.destroy();
    el.remove();
  });

  const rt = (md: string): string => {
    setMarkdown(editor, md);
    return toMarkdown(editor).trim();
  };

  it("headings", () => {
    expect(rt("# Title")).toBe("# Title");
    expect(rt("### Sub")).toBe("### Sub");
  });

  it("paragraph", () => {
    expect(rt("Just some text.")).toBe("Just some text.");
  });

  it("bold and italic", () => {
    expect(rt("**bold**")).toBe("**bold**");
    expect(rt("*italic*")).toBe("*italic*");
  });

  it("inline code", () => {
    expect(rt("Use `code` here.")).toBe("Use `code` here.");
  });

  it("bullet list", () => {
    const r = rt("- one\n- two");
    expect(r).toContain("- one");
    expect(r).toContain("- two");
  });

  it("ordered list", () => {
    const r = rt("1. first\n2. second");
    expect(r).toContain("1. first");
    expect(r).toContain("second");
  });

  it("blockquote", () => {
    expect(rt("> quoted")).toContain("> quoted");
  });

  it("horizontal rule", () => {
    expect(rt("above\n\n---\n\nbelow")).toContain("---");
  });

  it("link", () => {
    expect(rt("[text](https://example.com)")).toContain("[text](https://example.com)");
  });

  it("code block", () => {
    const r = rt("```\nline1\nline2\n```");
    expect(r).toContain("```");
    expect(r).toContain("line1");
    expect(r).toContain("line2");
  });

  it("table", () => {
    const r = rt("| A | B |\n| --- | --- |\n| 1 | 2 |");
    expect(r).toContain("| A | B |");
    expect(r).toContain("| 1 | 2 |");
  });

  it("empty document", () => {
    expect(rt("")).toBe("");
  });
});
