mergeInto(LibraryManager.library, {
    zooMirraLeaderboardGetScore: function (senderId, boardIdUtf8, callbackPtr) {
        const boardId = UTF8ToString(boardIdUtf8);
        const complete = (value) => Module.invokeMonoPCallback(senderId, callbackPtr, value);
        try {
            Promise.resolve(Module.mirraSDK.achievements.getScore(boardId))
                .then((score) => complete(Number.isFinite(score) ? Math.max(0, score | 0) : 0))
                .catch((error) => {
                    console.warn('[Leaderboards] GetScore failed for ' + boardId, error);
                    complete(-1);
                });
        } catch (error) {
            console.warn('[Leaderboards] GetScore failed for ' + boardId, error);
            complete(-1);
        }
    },

    zooMirraLeaderboardGetEntries: function (senderId, boardIdUtf8, callbackPtr) {
        const boardId = UTF8ToString(boardIdUtf8);
        const complete = (json) => {
            const jsonUtf8 = Module.allocateString(json || '');
            Module.invokeMonoPCallback(senderId, callbackPtr, jsonUtf8);
        };
        try {
            Promise.resolve(Module.mirraSDK.achievements.getLeaderboard(boardId))
                .then((leaderboard) => complete(JSON.stringify(leaderboard)))
                .catch((error) => {
                    console.warn('[Leaderboards] GetLeaderboard failed for ' + boardId, error);
                    complete('');
                });
        } catch (error) {
            console.warn('[Leaderboards] GetLeaderboard failed for ' + boardId, error);
            complete('');
        }
    },

    zooMirraLeaderboardSetScore: function (boardIdUtf8, score) {
        const boardId = UTF8ToString(boardIdUtf8);
        try {
            const operation = Module.mirraSDK.achievements.setScore(boardId, score);
            if (operation && typeof operation.catch === 'function') {
                operation.catch((error) => console.warn('[Leaderboards] SetScore failed for ' + boardId, error));
            }
        } catch (error) {
            console.warn('[Leaderboards] SetScore failed for ' + boardId, error);
        }
    }
});
