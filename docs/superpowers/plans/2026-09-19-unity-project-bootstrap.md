# Unity Project Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Привести Unity-проект с FishNet к чистой стартовой структуре, создать сжатую Markdown-документацию, опубликовать первый коммит и отдельно устранить подтверждённые ошибки CS0579.

**Architecture:** Unity-проект переносится из вложенной папки в корень репозитория, а воспроизводимые исходники отделяются от локальных кэшей и файлов IDE через `.gitignore`. Исходный ZIP используется только как неотслеживаемый источник для извлечения фактов; результатом является тематическая документация в `docs/`. Сначала публикуется чистая исходная база, затем исправление ошибок оформляется отдельным коммитом.

**Tech Stack:** Unity (фактическая версия из `ProjectSettings/ProjectVersion.txt`), FishNet, C#, Git/GitHub, Markdown, PDF/DOCX/XLSX-преобразование доступными локальными средствами.

**Spec:** `docs/superpowers/specs/2026-09-19-unity-project-bootstrap-design.md`

## Global Constraints

- Содержимое `My project/` переносится в корень `d:/LH_MAIN`.
- В Git входят `Assets/`, `Packages/`, `ProjectSettings/`, документация, `.gitignore` и корневой `README.md`.
- Не отслеживать `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.vs/`, `obj/`, `bin/`, `*.csproj`, `*.sln`, `*.user` и ZIP исходной документации.
- Не коммитить исходные PDF, DOCX и XLSX; хранить только их сжатое содержание в Markdown.
- Не менять Unity, FishNet и зависимости без подтверждённой необходимости для устранения ошибки.
- Публикация первого коммита предшествует диагностике и исправлению CS0579.

---

## File Structure

| Путь | Назначение |
|---|---|
| `/.gitignore` | Исключает Unity-кэши, артефакты IDE/сборки и исходный ZIP. |
| `/README.md` | Минимальный вход для разработчика: версия Unity, открытие проекта и ссылка на документы. |
| `/docs/README.md` | Индекс проектной документации. |
| `/docs/00-overview.md` | Видение игры, жанр, целевая сессия, границы концепта. |
| `/docs/01-gameplay/*.md` | Матч, персонаж, состояния, бой, инвентарь, мир и карта. |
| `/docs/02-content/*.md` | Оружие, модули, экипировка, транспорт, предметы и расходники. |
| `/docs/03-progression-economy/*.md` | Ресурсы, магазин, ключи, лутбоксы, премиальные механики. |
| `/docs/04-ui-social/*.md` | Лобби, интерфейс, распознавание и связь. |
| `/docs/05-technical/*.md` | Стратегические требования и технические решения/ограничения. |
| `/docs/superpowers/specs/2026-09-19-unity-project-bootstrap-design.md` | Согласованная спецификация. |
| `/docs/superpowers/plans/2026-09-19-unity-project-bootstrap.md` | Этот план. |

### Task 1: Сформировать чистое дерево Unity и правила Git

**Files:**
- Move: `My project/Assets/` -> `Assets/`
- Move: `My project/Packages/` -> `Packages/`
- Move: `My project/ProjectSettings/` -> `ProjectSettings/`
- Create: `.gitignore`
- Create: `README.md`
- Preserve locally, do not track: `Library/`, `Logs/`, `Temp/`, `UserSettings/`, `*.csproj`, `*.sln`

**Interfaces:**
- Consumes: Существующий Unity-проект в `My project/`.
- Produces: Стандартная корневая структура Unity, готовая к открытию Unity Hub и индексации Git.

- [ ] **Step 1: Зафиксировать исходный состав и версию Unity до переноса**

Run: `type "My project\ProjectSettings\ProjectVersion.txt" && type "My project\Packages\manifest.json"`

Expected: Отображаются версия Unity и зависимости, включая FishNet.

- [ ] **Step 2: Переместить только воспроизводимые каталоги Unity в корень**

Run: `move "My project\Assets" Assets && move "My project\Packages" Packages && move "My project\ProjectSettings" ProjectSettings`

Expected: В корне существуют `Assets/`, `Packages/`, `ProjectSettings/`; кэш и файлы IDE остаются вне индекса.

- [ ] **Step 3: Создать Unity-ориентированный `.gitignore`**

Создать файл со следующим обязательным содержимым:

```gitignore
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]in/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
.vs/
.vscode/
*.csproj
*.sln
*.suo
*.user
*.userprefs
*.pidb
*.booproj
*.tmp
Документация-*.zip
_documentation-source/
```

- [ ] **Step 4: Создать корневой `README.md`**

Содержимое должно включать:

```markdown
# LH_MAIN

Стартовый Unity-проект игры с сетевой библиотекой FishNet.

## Открытие проекта

1. Откройте корень репозитория через Unity Hub.
2. Используйте версию Unity из `ProjectSettings/ProjectVersion.txt`.
3. Дождитесь восстановления пакетов и генерации файлов IDE.

## Документация

Проектная документация находится в [docs/README.md](docs/README.md).
```

- [ ] **Step 5: Проверить структуру и игнорирование**

Run: `git init && git status --short --ignored`

Expected: `Assets/`, `Packages/`, `ProjectSettings/` видны как неотслеживаемые; Unity-кэши, `.csproj`, `.sln` и ZIP обозначены как ignored.

- [ ] **Step 6: Создать локальный коммит структуры только после завершения Tasks 2-3**

Run: `git add Assets Packages ProjectSettings .gitignore README.md docs && git commit -m "chore: bootstrap Unity project and documentation"`

Expected: Коммит включает воспроизводимую структуру и документацию, но не кэши и IDE-артефакты.

### Task 2: Извлечь факты из документации и создать Markdown-навигацию

**Files:**
- Create: `docs/README.md`
- Create: `docs/00-overview.md`
- Create: `docs/01-gameplay/*.md`
- Create: `docs/02-content/*.md`
- Create: `docs/03-progression-economy/*.md`
- Create: `docs/04-ui-social/*.md`
- Create: `docs/05-technical/*.md`
- Temporary, ignored: `_documentation-source/`

**Interfaces:**
- Consumes: `Документация-20260919T091548Z-1-001.zip`.
- Produces: Краткие взаимосвязанные Markdown-документы с индексом и открытыми вопросами.

- [ ] **Step 1: Извлечь архив в игнорируемую временную папку и инвентаризировать материалы**

Run: `mkdir _documentation-source && tar -xf "Документация-20260919T091548Z-1-001.zip" -C _documentation-source && tar -tf "Документация-20260919T091548Z-1-001.zip"`

Expected: Исходники доступны для чтения только в `_documentation-source/`, полный список файлов сохранён в выводе команды.

- [ ] **Step 2: Проверить доступные локальные преобразователи и выбрать минимальный путь извлечения**

Run: `where pdftotext && where python && where soffice && where 7z`

Expected: Определён инструмент для каждого из PDF, DOCX и XLSX. Если инструмент отсутствует, использовать поддерживаемый Python-пакет только после явного подтверждения его наличия; не добавлять его в проект Unity.

- [ ] **Step 3: Извлечь заголовки, таблицы и текст пакетно, сохраняя сырые результаты вне Git**

Для каждого источника создать текстовый файл в `_documentation-source/extracted/`. Требования к результату: имя соответствует исходнику; PDF разделён по страницам; DOCX сохраняет заголовки и абзацы; XLSX сохраняет имена листов, заголовки колонок и строки с игровыми данными.

Проверка: выбрать по одному PDF, DOCX и XLSX и сравнить наличие заголовков с оригиналом.

- [ ] **Step 4: Сгруппировать источники по доменам до написания Markdown**

Использовать соответствие:

```text
00-overview: gameProject_concept, Нарратив игры
01-gameplay: Блоки игры, правила рандомного матча, персонаж, состояния, рукопашный бой, гранаты, инвентарь, камера, динамические объекты, карта, SpawnPoint
02-content: оружие, пулы оружия/модулей, ножи, экипировка, транспорт, расходники
03-progression-economy: сервис купли-продажи, лутбоксы, премиальные бонусы, ключи, ресурсы/премиум/страховка
04-ui-social: лобби, распознавание и связь
05-technical: стратегические требования, сетевые и платформенные ограничения из остальных материалов
```

Проверка: каждый файл из архива относится ровно к одному основному домену; межсистемные факты могут быть ссылками, но не копируются целиком.

- [ ] **Step 5: Создать тематические Markdown-файлы по единому шаблону**

Каждый файл должен иметь форму:

```markdown
# <Название системы>

## Назначение

## Ключевые правила

## Сущности и контент

## Зависимости

## Открытые вопросы

## Источники
```

Требования: передавать подтверждённые правила без домыслов; убрать повторы; противоречия и недостающие решения писать в «Открытые вопросы»; в «Источники» перечислять названия исходных файлов без помещения самих файлов в Git.

- [ ] **Step 6: Создать `docs/README.md` и `docs/00-overview.md`**

`docs/README.md` обязан содержать ссылки на каждый тематический файл. `docs/00-overview.md` должен консолидировать видение, игровой жанр, основной цикл, аудиторию/платформы только когда это прямо задано первоисточником, и ссылаться на детальные системы.

- [ ] **Step 7: Проверить полноту и отсутствие исходников в индексе**

Run: `git status --short && git check-ignore -v "Документация-20260919T091548Z-1-001.zip" "_documentation-source\any-file.txt"`

Expected: Markdown-документы видны как новые; ZIP и временные извлечённые файлы игнорируются.

### Task 3: Создать и опубликовать чистый стартовый коммит

**Files:**
- Modify: `.gitignore`
- Add: `Assets/`, `Packages/`, `ProjectSettings/`, `docs/`, `README.md`

**Interfaces:**
- Consumes: Корневая структура из Task 1 и Markdown-документация из Task 2.
- Produces: Опубликованный стартовый коммит в `https://github.com/AleksandrVoinitsky/LH_MAIN.git`.

- [ ] **Step 1: Провести аудит индекса до первого коммита**

Run: `git status --short --ignored && git add -n Assets Packages ProjectSettings docs .gitignore README.md`

Expected: В предполагаемом наборе отсутствуют `Library`, `Temp`, `Logs`, `UserSettings`, `obj`, `bin`, `.vs`, `*.csproj`, `*.sln`, ZIP и `_documentation-source`.

- [ ] **Step 2: Инициализировать репозиторий, настроить ветку и remote**

Run: `git init && git branch -M main && git remote add origin https://github.com/AleksandrVoinitsky/LH_MAIN.git && git remote -v`

Expected: `origin` имеет одинаковые fetch/push URL предоставленного GitHub-репозитория.

- [ ] **Step 3: Создать первый коммит**

Run: `git add Assets Packages ProjectSettings docs .gitignore README.md && git commit -m "chore: bootstrap Unity project and documentation" && git show --stat --oneline HEAD`

Expected: Коммит создан; статистика не содержит игнорируемых каталогов и бинарных источников документации.

- [ ] **Step 4: Опубликовать ветку `main`**

Run: `git push -u origin main`

Expected: GitHub принимает коммит, локальная `main` отслеживает `origin/main`.

- [ ] **Step 5: Проверить опубликованный результат**

Run: `git status --short --branch && git ls-remote origin refs/heads/main`

Expected: Рабочее дерево чисто; локальный HEAD соответствует хешу удалённой ветки `main`.

### Task 4: Диагностировать и устранить пять ошибок CS0579 отдельным изменением

**Files:**
- Possible modify, only if evidence requires: конкретный `AssemblyInfo.cs`, `.asmdef` или файл конфигурации, установленный причиной.
- Do not add: `obj/`, `bin/`, `Library/`, `.csproj`, `.sln`.
- Create only if useful: `docs/05-technical/build-troubleshooting.md`

**Interfaces:**
- Consumes: Чистый опубликованный стартовый коммит и точный вывод пяти ошибок из Unity Console.
- Produces: Воспроизводимое отсутствие конфликтов атрибутов сборки и отдельный коммит исправления, если причина находится в отслеживаемом содержимом.

- [ ] **Step 1: Получить точные пять ошибок из Unity Console и их полный стек/пути**

В Unity Console включить `Error Pause` и скопировать текст каждой ошибки, включая путь и номера строк. Сгруппировать одинаковые сообщения и не считать повтор одной ошибки разными причинами.

Expected: Есть перечень из пяти сообщений, источник каждого и воспроизводимый сценарий появления.

- [ ] **Step 2: Проверить конфликтующие определения без изменений**

Run: `rg -n --glob "!Library/**" --glob "!Temp/**" --glob "!obj/**" --glob "!bin/**" "\[assembly:\s*(AssemblyCompany|AssemblyConfiguration|AssemblyFileVersion|AssemblyInformationalVersion|AssemblyProduct|AssemblyTitle|AssemblyVersion)" .`

Expected: Найдены все вручную определённые assembly-атрибуты в отслеживаемом проекте; результаты сопоставлены с путями ошибок.

- [ ] **Step 3: Проверить, что сгенерированные артефакты действительно не отслеживаются**

Run: `git ls-files | findstr /i /r "\\obj\\ \\bin\\ \\Library\\ \.csproj$ \.sln$"`

Expected: Команда не выводит сгенерированные файлы. Если вывод есть, удалить их только из Git-индекса через `git rm --cached`, не удаляя локальные данные.

- [ ] **Step 4: Очистить только пересоздаваемые локальные данные и перегенерировать проект**

Закрыть Unity и IDE. Удалить локальные `obj/`, `bin/`, `Library/ScriptAssemblies/` и сгенерированные `.csproj`/`.sln`, затем открыть проект Unity через Hub и дождаться завершения импорта.

Expected: Unity заново создаёт IDE-проекты и кэш компиляции; `git status` не меняется.

- [ ] **Step 5: Устранить подтверждённый первичный источник, если ошибки остались**

Править только файл, совпадающий с путём и типом дубликата из Step 1-2. Предпочтение: удалить вручную заданный assembly-атрибут, если тот же атрибут корректно генерирует SDK; не отключать генерацию глобально и не удалять сторонний пакет без доказательства.

Expected: После повторной компиляции отсутствуют все пять CS0579.

- [ ] **Step 6: Проверить и зафиксировать исправление отдельным коммитом**

Run: `git diff --check && git status --short && git add <только-подтверждённые-файлы> && git commit -m "fix: resolve duplicate assembly attributes" && git push`

Expected: В коммите нет временных или сгенерированных файлов; удалённая ветка обновлена.

## План самопроверки

- Спецификация покрыта: перенос Unity и игнорирование — Task 1; сжатая документация — Task 2; GitHub и первый коммит — Task 3; диагностика и отдельное исправление CS0579 — Task 4.
- В плане нет намеренных изменений версий Unity, FishNet или пакетов.
- Исходные PDF/DOCX/XLSX и ZIP остаются вне Git.
- Выполнение Task 4 не начинается до успешной публикации Task 3.
