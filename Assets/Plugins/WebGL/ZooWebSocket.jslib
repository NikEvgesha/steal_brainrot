mergeInto(LibraryManager.library, {
  ZooWebSocketConnect: function (urlPtr, receiverPtr) {
    try {
      var previous = window.__zooLobbyWebSocket;
      if (previous) {
        previous.onopen = null;
        previous.onmessage = null;
        previous.onerror = null;
        previous.onclose = null;
        try { previous.close(); } catch (_) {}
      }

      var url = UTF8ToString(urlPtr);
      var receiver = UTF8ToString(receiverPtr);
      var socket = new WebSocket(url);
      window.__zooLobbyWebSocket = socket;

      socket.onopen = function () {
        if (window.__zooLobbyWebSocket !== socket) return;
        SendMessage(receiver, "OnWebSocketOpenedFromJs", "");
      };

      socket.onmessage = function (event) {
        if (window.__zooLobbyWebSocket !== socket) return;
        if (typeof event.data === "string") {
          SendMessage(receiver, "OnWebSocketMessageFromJs", event.data);
        }
      };

      socket.onerror = function () {
        if (window.__zooLobbyWebSocket !== socket) return;
        SendMessage(receiver, "OnWebSocketErrorFromJs", "browser_error");
      };

      socket.onclose = function (event) {
        if (window.__zooLobbyWebSocket !== socket) return;
        window.__zooLobbyWebSocket = null;
        SendMessage(
          receiver,
          "OnWebSocketClosedFromJs",
          String(event.code) + ":" + (event.reason || "")
        );
      };

      return 1;
    } catch (_) {
      window.__zooLobbyWebSocket = null;
      return 0;
    }
  },

  ZooWebSocketSend: function (messagePtr) {
    try {
      var socket = window.__zooLobbyWebSocket;
      if (!socket || socket.readyState !== WebSocket.OPEN) return 0;
      socket.send(UTF8ToString(messagePtr));
      return 1;
    } catch (_) {
      return 0;
    }
  },

  ZooWebSocketClose: function () {
    var socket = window.__zooLobbyWebSocket;
    window.__zooLobbyWebSocket = null;
    if (!socket) return;

    socket.onopen = null;
    socket.onmessage = null;
    socket.onerror = null;
    socket.onclose = null;
    try { socket.close(1000, "client_close"); } catch (_) {}
  }
});
