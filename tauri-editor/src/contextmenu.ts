import { Editor } from "@tiptap/core";
import { commands } from "./commands";
import { addToDictionary } from "./preferences";

/**
 * Right-click behaviour.
 *
 * WebView2 shows its own native spelling menu (suggestions + the OS's own entries)
 * when you right-click a misspelled word. We do NOT suppress that — it is the best
 * source of suggestions. We only show a custom menu when the click is inside a table
 * (for row/column/header ops) or offer "Add to Dictionary" for the word under the
 * cursor. The custom menu is used when the user right-clicks with a table active.
 */
export function installContextMenu(editor: Editor, root: HTMLElement, menuEl: HTMLElement): void {
  const editable = root.querySelector(".ProseMirror") as HTMLElement;

  editable.addEventListener("contextmenu", (e: MouseEvent) => {
    const inTable = commands.inTable(editor);
    const word = wordUnderCursor();

    // If not in a table and there is no misspell-add case we care about, let the
    // native menu (with spelling suggestions) show.
    if (!inTable && !word) return;

    e.preventDefault();
    buildMenu(menuEl, e.clientX, e.clientY, [
      ...(word
        ? [
            {
              label: `Add "${word}" to Dictionary`,
              action: async () => {
                await addToDictionary(word);
                // Re-check spelling by nudging the editable's spellcheck.
                editable.setAttribute("spellcheck", "false");
                requestAnimationFrame(() => editable.setAttribute("spellcheck", "true"));
              },
            },
            { separator: true as const },
          ]
        : []),
      ...(inTable
        ? [
            { label: "Insert Row Above", action: () => commands.addRowBefore(editor) },
            { label: "Insert Row Below", action: () => commands.addRowAfter(editor) },
            { label: "Delete Row", action: () => commands.deleteRow(editor) },
            { separator: true as const },
            { label: "Insert Column Left", action: () => commands.addColumnBefore(editor) },
            { label: "Insert Column Right", action: () => commands.addColumnAfter(editor) },
            { label: "Delete Column", action: () => commands.deleteColumn(editor) },
            { separator: true as const },
            { label: "Toggle Header Row", action: () => commands.toggleHeaderRow(editor) },
          ]
        : []),
    ]);
  });

  // Dismiss the custom menu on any outside click / Escape.
  document.addEventListener("click", () => hide(menuEl));
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") hide(menuEl);
  });
}

type Item = { label: string; action: () => void } | { separator: true };

function buildMenu(menuEl: HTMLElement, x: number, y: number, items: Item[]): void {
  menuEl.innerHTML = "";
  for (const item of items) {
    if ("separator" in item) {
      const sep = document.createElement("div");
      sep.className = "ctx-sep";
      menuEl.appendChild(sep);
    } else {
      const btn = document.createElement("div");
      btn.className = "ctx-item";
      btn.textContent = item.label;
      btn.addEventListener("click", () => {
        item.action();
        hide(menuEl);
      });
      menuEl.appendChild(btn);
    }
  }
  menuEl.style.left = `${x}px`;
  menuEl.style.top = `${y}px`;
  menuEl.hidden = false;
}

function hide(menuEl: HTMLElement): void {
  menuEl.hidden = true;
}

/** Best-effort word under the caret, using the current selection/caret range. */
function wordUnderCursor(): string | null {
  const sel = window.getSelection();
  if (!sel || sel.rangeCount === 0) return null;
  const node = sel.anchorNode;
  if (!node || node.nodeType !== Node.TEXT_NODE) return null;
  const text = node.textContent ?? "";
  const offset = sel.anchorOffset;

  let start = offset;
  let end = offset;
  const isWord = (c: string) => /[A-Za-z'’-]/.test(c);
  while (start > 0 && isWord(text[start - 1])) start--;
  while (end < text.length && isWord(text[end])) end++;
  const word = text.slice(start, end).replace(/^['’-]+|['’-]+$/g, "");
  return word.length > 1 ? word : null;
}
