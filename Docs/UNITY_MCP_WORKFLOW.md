# Official Unity MCP workflow

Проект использует официальный Unity MCP из пакета `com.unity.ai.assistant`.

## Подключение

- Требуется Unity 6 или новее.
- Unity Editor должен быть открыт на этом проекте.
- MCP relay на Windows: `%USERPROFILE%\.unity\relay\relay_win.exe`.
- Аргументы клиента: `--mcp --project-path E:\GitFork\steal_brainrot`.
- Конфигурация Cursor хранится в `.cursor/mcp.json`.
- После изменения MCP-конфигурации перезапустите MCP-клиент. Если relay не видит Editor, перезапустите Unity.

Unity автоматически публикует локальное подключение через именованный канал. Путь к активному проекту передаётся явно, поэтому relay не должен подключиться к другому открытому Unity-проекту.

## Минимальная проверка изменений

1. Дождаться, пока Editor закончит импорт и компиляцию.
2. Получить состояние Editor и убедиться, что `isCompiling` равен `false`.
3. Прочитать Console и проверить отсутствие новых errors/warnings.
4. Проверить активную сцену и нужную часть иерархии.
5. При изменении C# запустить валидацию затронутых скриптов.
6. Для игровой задачи запустить Play Mode, пройти сценарий и снять Game/Scene View.
7. Остановить Play Mode и повторно прочитать Console.
8. Проверить `git diff --check` и рабочее дерево.

## Практические ограничения

- Не вызывайте `get_components` для `Canvas` и объектов внутри UI-иерархии. В документации пакета `2.17.0-pre.1` это отмечено как известная причина зависания или падения Editor для `Canvas`, `CanvasScaler`, `GraphicRaycaster` и `RectTransform`.
- `Unity_RunCommand` выполняет произвольный Editor C# и способен менять проект. Перед записью нужен Git checkpoint, а команда должна иметь узкую область действия.
- Во время импорта, сборки или компиляции ответы MCP могут быть медленными. Дождитесь завершения Editor-операции.
- Генераторы ассетов и Assistant относятся к отдельным возможностям Unity AI. Бесплатность MCP не означает бесплатную генерацию ассетов.

Официальные материалы:

- [Unity MCP: how to get started](https://unity.com/blog/unity-ai-mcp-how-to-get-started)
- [Unity AI feature and pricing FAQ](https://unity.com/features/ai)
- [Unity MCP documentation](https://docs.unity3d.com/Packages/com.unity.ai.assistant@latest/index.html?subfolder=/manual/integration/unity-mcp-get-started.html)
- [Unity MCP troubleshooting](https://docs.unity3d.com/Packages/com.unity.ai.assistant@latest/index.html?subfolder=/manual/troubleshoot/unity-mcp-troubleshooting.html)
