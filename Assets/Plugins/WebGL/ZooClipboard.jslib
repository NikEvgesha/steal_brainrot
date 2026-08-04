mergeInto(LibraryManager.library, {
  ZooClipboardCopyText: function (textPtr) {
    var text = UTF8ToString(textPtr);

    function copyWithSelection() {
      var textArea = null;
      try {
        textArea = document.createElement("textarea");
        textArea.value = text;
        textArea.setAttribute("readonly", "");
        textArea.style.position = "fixed";
        textArea.style.left = "-9999px";
        textArea.style.top = "0";
        textArea.style.opacity = "0";
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        textArea.setSelectionRange(0, textArea.value.length);
        return document.execCommand("copy");
      } catch (_) {
        return false;
      } finally {
        if (textArea && textArea.parentNode) {
          textArea.parentNode.removeChild(textArea);
        }
      }
    }

    // execCommand still works in cross-origin game iframes where the modern
    // Clipboard API can be hidden by the host's Permissions Policy.
    if (copyWithSelection()) return 1;

    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).catch(function (error) {
          console.warn("[Friends] Clipboard write was rejected", error);
        });
        return 1;
      }
    } catch (_) {}

    return 0;
  }
});
