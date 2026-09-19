# Game Development Roadmap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Вести разработку игры последовательными проверяемыми кодовыми фазами от контейнерной основы до эксплуатации.

**Architecture:** Каждая фаза дорожной карты является отдельным под-проектом со своей спецификацией и исполнимым планом. Постоянные правила находятся в `docs/06-architecture/`, а порядок и критерии — в `docs/07-development/`.

**Tech Stack:** Unity 6000.3.14f1, FishNet 4.7.3, C#, ASP.NET Core, PostgreSQL, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-09-19-game-development-roadmap-design.md`

## Global Constraints

- Разрабатывается только код, сервисы, тесты, контейнеризация и технические конфигурации; карты и контентные ассеты не создаются.
- Unity dedicated server является авторитетом матча; backend владеет постоянными данными.
- Использовать FishNet для сети, не реализовывать самописную репликацию или транспорт.
- Локальная среда использует Docker Compose с PostgreSQL, `backend-api` и двумя заранее запущенными game-server.
- Целевой контракт матча — до 40 игроков; каждая сетевая фаза подтверждается измерением.

---

### Task 1: Реализовать фазу 00 — основу разработки

**Files:**
- Follow: `docs/07-development/phase-00-foundation.md`
- Read: `docs/06-architecture/*.md`

- [ ] Создать отдельную спецификацию фазы 00 с точным расположением C#-проектов, Compose-файлов и CI.
- [ ] Создать исполнимый TDD-план фазы 00.
- [ ] Реализовать только утверждённый каркас, тесты и Compose.
- [ ] Проверить критерии из `definition-of-done.md` и оформить отдельный коммит.

### Task 2: Реализовать фазу 01 — backend и данные

**Files:**
- Follow: `docs/07-development/phase-01-backend.md`
- Depends on: Task 1

- [ ] Уточнить API/схему и написать спецификацию фазы 01.
- [ ] Создать TDD-план и реализовать identity/profile/migrations.
- [ ] Выполнить интеграционные проверки с PostgreSQL в Compose.
- [ ] Проверить критерии готовности и оформить отдельный коммит.

### Task 3: Реализовать фазы 02–10 последовательно

**Files:**
- Follow: `docs/07-development/phase-02-dedicated-server.md` through `phase-10-operations.md`

- [ ] Перед началом каждой фазы проверить её зависимости по `roadmap.md`.
- [ ] Для каждой фазы отдельно: согласовать спецификацию, создать TDD-план, реализовать код, выполнить фазовые проверки и закоммитить результат.
- [ ] Не переходить к следующей фазе при невыполненных критериях текущей.

## План самопроверки

- Порядок работ и зависимости соответствуют `docs/07-development/roadmap.md`.
- Детальные технические решения намеренно откладываются до спецификации соответствующей фазы.
- Во всех фазах применяются глобальные ограничения спецификации.
