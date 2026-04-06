# 🏗️ Архитектура серверного эмулятора ArcheAge 3.0

> **Версия:** 3.0.3.0-alpha  
> **Платформа:** .NET 6.0 (C# 10)  
> **Лицензия:** Dual MIT / GPL  

---

## 📋 Содержание

1. [Структура проекта](#-структура-проекта)
2. [Обзор архитектуры](#-обзор-архитектуры)
3. [Сетевая архитектура](#-сетевая-архитектура)
4. [Система пакетов](#-система-пакетов)
5. [Система менеджеров](#-система-менеджеров-singleton-паттерн)
6. [Структура игровых моделей](#-структура-игровых-моделей)
7. [Система команд](#-система-команд)
8. [Архитектура базы данных](#-архитектура-базы-данных)
9. [Зависимости (NuGet)](#-зависимости-nuget)
10. [Система логирования](#-система-логирования)
11. [Конфигурационные файлы](#-конфигурационные-файлы)
12. [Поток аутентификации](#-поток-аутентификации)

---

## 📁 Структура проекта

```
archeage3.0/
├── AAEmu.sln                    # Файл решения Visual Studio
├── AAEmu.sln.DotSettings        # Настройки ReSharper
│
├── AAEmu.Commons/               # Общая библиотека (shared library)
│   ├── AAEmu.Commons.csproj     # TargetFramework: net6.0
│   ├── Cryptography/            # Шифрование пакетов
│   ├── Models/                  # Базовые модели данных
│   ├── Network/                 # Базовые сетевые классы
│   └── Utils/                   # Утилиты (Helpers, MySqlDb и т.д.)
│
├── AAEmu.Game/                  # Игровой сервер (Game Server)
│   ├── AAEmu.Game.csproj        # TargetFramework: net6.0
│   ├── GameService.cs           # Главный сервис запуска
│   ├── Core/                    # Менеджеры, сеть, пакеты
│   ├── Models/                  # Игровые модели
│   ├── Scripts/                 # Скрипты и команды
│   └── Data/                    # Конфигурации и данные
│
├── AAEmu.Login/                 # Сервер авторизации (Login Server)
│   ├── AAEmu.Login.csproj       # TargetFramework: net6.0
│   ├── LoginService.cs          # Главный сервис запуска
│   ├── Core/                    # Менеджеры, сеть, пакеты
│   └── Models/                  # Модели данных
│
├── SQL/                         # SQL-скрипты базы данных
│   ├── aaemu_login/             # Схема БД авторизации
│   └── aaemu_game/              # Схема БД игрового мира
│
├── doc/                         # Документация
├── publish.sh                   # Скрипт сборки (Linux)
├── StartGameServer.bat          # Запуск игрового сервера (Windows)
├── StartLoginServer.bat         # Запуск сервера авторизации (Windows)
└── start_AAEmu.bat              # Запуск обоих серверов
```

### Проекты решения

| Проект | Тип | Описание |
|--------|-----|----------|
| **AAEmu.Commons** | Библиотека классов | Общие утилиты: сеть, БД, шифрование, логирование |
| **AAEmu.Game** | Исполняемый файл | Игровой сервер — вся игровая логика |
| **AAEmu.Login** | Исполняемый файл | Сервер авторизации — аутентификация и список миров |

> Оба сервера используют архитектуру `Microsoft.Extensions.Hosting` (IHostedService)
> и поддерживают `AllowUnsafeBlocks` для работы с неуправляемой памятью.

---

## 🏛️ Обзор архитектуры

### Двусерверная архитектура

Эмулятор состоит из двух независимых серверных процессов, связанных внутренней TCP-сетью:

```
┌─────────────────────────────────────────────────────────────────────┐
│                        КЛИЕНТ ARCHEAGE                             │
│                    (Официальный клиент 3.0)                        │
└──────────┬──────────────────────────────────────┬───────────────────┘
           │ TCP :1237                            │ TCP :1239 / :1250
           ▼                                      ▼
┌─────────────────────┐    TCP :1234    ┌────────────────────────────┐
│                     │◄───────────────►│                            │
│   LOGIN SERVER      │   Внутренняя    │      GAME SERVER           │
│   (AAEmu.Login)     │     сеть        │      (AAEmu.Game)          │
│                     │                 │                            │
│ • Аутентификация    │                 │ • Игровая логика           │
│ • Список миров      │                 │ • Управление персонажами   │
│ • Управление        │                 │ • Боевая система           │
│   аккаунтами        │                 │ • Квесты, крафт, торговля  │
│                     │                 │ • Жилищная система         │
│                     │                 │ • NPC / AI                 │
└─────────┬───────────┘                 └──────────┬─────────────────┘
          │                                        │
          ▼                                        ▼
┌─────────────────────┐                 ┌────────────────────────────┐
│  MySQL: aaemu_login │                 │  MySQL: aaemu_game         │
│  • accounts         │                 │  • characters              │
│  • game_servers     │                 │  • items, housing, quests  │
│  Порт: 3306         │                 │  Порт: 3306                │
└─────────────────────┘                 │                            │
                                        │  SQLite: compact.sqlite3   │
                                        │  • Данные игры (только чт.)│
                                        └────────────────────────────┘
```

### Принцип работы

1. **Login Server** — «привратник». Принимает подключения клиентов, выполняет аутентификацию,
   отдаёт список доступных игровых миров и перенаправляет игрока на Game Server.

2. **Game Server** — «движок мира». Обрабатывает всю игровую логику: перемещение, бой, квесты,
   крафт, торговлю, жилищную систему, NPC, AI и многое другое.

3. **Внутренняя сеть** — TCP-канал между серверами для передачи данных об аутентификации,
   регистрации игровых серверов и переподключении игроков.

---

## 🌐 Сетевая архитектура

### Таблица портов и подключений

```
┌──────────────────┬─────────────┬───────┬──────────────────────────────────────┐
│    Компонент     │    Хост     │ Порт  │            Описание                  │
├──────────────────┼─────────────┼───────┼──────────────────────────────────────┤
│ Login Server     │ * (0.0.0.0) │ 1237  │ Подключения клиентов для             │
│ (клиентская)     │             │       │ аутентификации                       │
├──────────────────┼─────────────┼───────┼──────────────────────────────────────┤
│ Game Server      │ 127.0.0.1   │ 1239  │ Основные игровые подключения         │
│ (клиентская)     │             │       │ клиентов                             │
├──────────────────┼─────────────┼───────┼──────────────────────────────────────┤
│ Stream Network   │ 127.0.0.1   │ 1250  │ Потоковая передача данных            │
│                  │             │       │ (эмблемы, имена и пр.)               │
├──────────────────┼─────────────┼───────┼──────────────────────────────────────┤
│ Internal Network │ 127.0.0.1   │ 1234  │ Внутренняя связь Login ↔ Game        │
│                  │             │       │ (аутентификация, переподключения)     │
├──────────────────┼─────────────┼───────┼──────────────────────────────────────┤
│ MySQL Database   │ localhost   │ 3306  │ База данных (aaemu_login,            │
│                  │             │       │ aaemu_game)                           │
└──────────────────┴─────────────┴───────┴──────────────────────────────────────┘
```

### Схема сетевого взаимодействия

```
                    ┌──────────┐
                    │  Клиент  │
                    └────┬─┬───┘
                         │ │
            ┌────────────┘ └────────────┐
            │ :1237                     │ :1239          :1250
            ▼                           ▼                  ▼
   ┌─────────────────┐        ┌──────────────────┐  ┌───────────────┐
   │  LoginNetwork   │        │   GameNetwork    │  │ StreamNetwork │
   │  (C2L / L2C)    │        │   (C2G / G2C)    │  │  (C2S / S2C)  │
   └────────┬────────┘        └────────┬─────────┘  └───────────────┘
            │                          │
            │         :1234            │
            └──────────┬───────────────┘
                       ▼
              ┌─────────────────┐
              │ InternalNetwork │
              │   (L2G / G2L)   │
              └─────────────────┘
```

> **Примечание:** Login Server слушает на `*` (0.0.0.0) — принимает подключения с любого адреса.
> Game Server и Stream Network привязаны к `127.0.0.1` — только локальные подключения
> (для продакшна адреса настраиваются в `Config.json`).

---

## 📦 Система пакетов

### Типы пакетов

Система пакетов разделена на 7 направлений в зависимости от отправителя и получателя:

```
          Клиент                    Login Server                 Game Server
        ┌────────┐               ┌──────────────┐             ┌─────────────┐
        │        │──── C2L ─────►│              │             │             │
        │        │◄──── L2C ─────│              │── L2G ─────►│             │
        │        │               │              │◄──── G2L ───│             │
        │        │──── C2G ──────────────────────────────────►│             │
        │        │◄──── G2C ──────────────────────────────────│             │
        │        │──── C2S ──────────────────────────────────►│  (Stream)   │
        │        │◄──── S2C ──────────────────────────────────│  (Stream)   │
        └────────┘               └──────────────┘             └─────────────┘
```

| Префикс | Направление | Расположение | Описание |
|----------|-------------|--------------|----------|
| **C2L** | Клиент → Login | `AAEmu.Login/Core/Packets/C2L/` | Запросы аутентификации |
| **L2C** | Login → Клиент | `AAEmu.Login/Core/Packets/L2C/` | Ответы авторизации |
| **C2G** | Клиент → Game | `AAEmu.Game/Core/Packets/C2G/` | Игровые команды клиента |
| **G2C** | Game → Клиент | `AAEmu.Game/Core/Packets/G2C/` | Обновления игрового мира |
| **L2G** | Login → Game | `AAEmu.Login/Core/Packets/L2G/` | Внутренние: вход игрока |
| **G2L** | Game → Login | `AAEmu.Game/Core/Packets/G2L/` | Внутренние: регистрация |
| **C2S** | Клиент → Stream | `AAEmu.Game/Core/Packets/C2S/` | Потоковые данные |
| **S2C** | Stream → Клиент | `AAEmu.Game/Core/Packets/S2C/` | Потоковые ответы |
| **Proxy** | Прокси | `AAEmu.Game/Core/Packets/Proxy/` | Прокси-пакеты |

### Базовые классы пакетов

```
PacketMarshaler (Commons)
    └── PacketBase<T> (Commons)
            ├── LoginPacket : PacketBase<LoginConnection>
            │       Методы: Read(), Execute()
            │
            └── GamePacket : PacketBase<GameConnection>
                    Свойства: Level (уровень безопасности)
                    Методы: Read(), Execute(), Encode()
                    Шифрование: встроено в Encode()
```

### Регистрация пакетов Login Server

```csharp
// CARequestAuthPacket      0x01  — Основная аутентификация
// CARequestAuthTencentPacket 0x02 — Аутентификация Tencent
// CARequestAuthGameOnPacket  0x03 — Аутентификация GameOn
// CARequestAuthTrionPacket   0x04 — Аутентификация Trion
// CAChallengeResponsePacket  0x05 — Ответ на challenge
// CAChallengeResponse2Packet 0x06 — Ответ на challenge v2
// CAListWorldPacket          0x0C — Запрос списка миров
// CAEnterWorldPacket         0x0D — Вход в выбранный мир
// CARequestReconnectPacket   0x0F — Переподключение
// CARequestAuthTWPacket      0x11 — Аутентификация (Тайвань)
```

---

## ⚙️ Система менеджеров (Singleton-паттерн)

Все менеджеры реализованы как синглтоны и загружаются в строго определённом порядке
при запуске `GameService.StartAsync()`. Всего **57+ менеджеров**, организованных в 7 фаз.

### Диаграмма порядка инициализации

```
╔══════════════════════════════════════════════════════════════════════╗
║                    ЗАПУСК GAMESERVICE                                ║
╚══════════════════════════════════════════════════════════════════════╝
                              │
         ┌────────────────────▼────────────────────┐
         │     ФАЗА 1: МЕНЕДЖЕРЫ ИДЕНТИФИКАТОРОВ   │
         │─────────────────────────────────────────│
         │  TaskIdManager.Initialize()              │
         │  TaskManager.Initialize()                │
         │  LocalizationManager.Load()              │
         │  ObjectIdManager.Initialize()            │
         │  TradeIdManager.Initialize()             │
         └────────────────────┬────────────────────┘
                              │
         ┌────────────────────▼────────────────────┐
         │     ФАЗА 2: ОСНОВНЫЕ МЕНЕДЖЕРЫ          │
         │─────────────────────────────────────────│
         │  ItemIdManager          ChatManager      │
         │  CharacterIdManager     FamilyIdManager  │
         │  ExpeditionIdManager                     │
         │  VisitedSubZoneIdManager                 │
         │  PrivateBookIdManager   FriendIdManager  │
         │  MateIdManager          HousingIdManager │
         │  HousingTldManager      TeamIdManager    │
         │  LaborPowerManager      QuestIdManager   │
         └────────────────────┬────────────────────┘
                              │
         ┌────────────────────▼────────────────────┐
         │     ФАЗА 3: МИР И КВЕСТЫ               │
         │─────────────────────────────────────────│
         │  ZoneManager.Load()                      │
         │  WorldManager.Load()                     │
         │  ┌─ WorldManager.LoadHeightmaps() ──┐   │
         │  │  (асинхронная загрузка карт высот)│   │
         │  └──────────────────────────────────┘   │
         │  QuestManager.Load()                     │
         │  ShipyardManager.Load()                  │
         └────────────────────┬────────────────────┘
                              │
         ┌────────────────────▼────────────────────┐
         │     ФАЗА 4: ИГРОВЫЕ СИСТЕМЫ            │
         │─────────────────────────────────────────│
         │  FormulaManager     ExpirienceManager    │
         │  ConfigurationManager  TlIdManager       │
         │  SpecialtyManager   ItemManager (×2)     │
         │  AnimationManager   PlotManager          │
         │  SkillManager       CraftManager         │
         │  MateManager        SlaveManager         │
         │  TeamManager        AuctionManager       │
         │  MailManager        NameManager           │
         │  FactionManager     ExpeditionManager    │
         │  CharacterManager   FamilyManager        │
         │  PortalManager      FriendMananger       │
         │  NpcManager         DoodadManager        │
         │  HousingManager     TransferManager      │
         │  GimmickManager                          │
         └────────────────────┬────────────────────┘
                              │
         ┌────────────────────▼────────────────────┐
         │     ФАЗА 5: СПАВН ОБЪЕКТОВ             │
         │─────────────────────────────────────────│
         │  ← Ожидание загрузки карт высот         │
         │  SpawnManager.Load()                     │
         │  SpawnManager.SpawnAll()                  │
         │  HousingManager.SpawnAll()               │
         └────────────────────┬────────────────────┘
                              │
         ┌────────────────────▼────────────────────┐
         │     ФАЗА 6: СЕРВИСЫ                     │
         │─────────────────────────────────────────│
         │  AccessLevelManager  CashShopManager     │
         │  ScriptCompiler.Compile()                │
         │  SaveManager         SpecialtyManager    │
         │  BoatPhysicsManager  TransferManager     │
         │  GimmickManager      SlaveManager        │
         │  EncryptionManager   TimeManager         │
         │  TaskManager.Start()                     │
         └────────────────────┬────────────────────┘
                              │
         ┌────────────────────▼────────────────────┐
         │     ФАЗА 7: ЗАПУСК СЕТИ                 │
         │─────────────────────────────────────────│
         │  GameNetwork.Start()     → :1239         │
         │  StreamNetwork.Start()   → :1250         │
         │  LoginNetwork.Start()    → :1234         │
         └─────────────────────────────────────────┘
                              │
                              ▼
                    ╔════════════════╗
                    ║  СЕРВЕР ГОТОВ  ║
                    ╚════════════════╝
```

### Полный список менеджеров по фазам

<details>
<summary><b>Фаза 1 — Менеджеры идентификаторов (5 менеджеров)</b></summary>

| № | Менеджер | Метод | Описание |
|---|----------|-------|----------|
| 1 | `TaskIdManager` | Initialize() | Идентификаторы задач |
| 2 | `TaskManager` | Initialize() | Управление задачами |
| 3 | `LocalizationManager` | Load() | Локализация текстов |
| 4 | `ObjectIdManager` | Initialize() | ID игровых объектов |
| 5 | `TradeIdManager` | Initialize() | ID торговых операций |

</details>

<details>
<summary><b>Фаза 2 — Основные ID-менеджеры (14 менеджеров)</b></summary>

| № | Менеджер | Описание |
|---|----------|----------|
| 6 | `ItemIdManager` | ID предметов |
| 7 | `ChatManager` | Система чата |
| 8 | `CharacterIdManager` | ID персонажей |
| 9 | `FamilyIdManager` | ID семей |
| 10 | `ExpeditionIdManager` | ID экспедиций/гильдий |
| 11 | `VisitedSubZoneIdManager` | ID посещённых подзон |
| 12 | `PrivateBookIdManager` | ID личных книг |
| 13 | `FriendIdManager` | ID друзей |
| 14 | `MateIdManager` | ID спутников/маунтов |
| 15 | `HousingIdManager` | ID жилья |
| 16 | `HousingTldManager` | Области жилищной зоны |
| 17 | `TeamIdManager` | ID групп/рейдов |
| 18 | `LaborPowerManager` | Система очков работы |
| 19 | `QuestIdManager` | ID квестов |

</details>

<details>
<summary><b>Фаза 3 — Мир (4 менеджера)</b></summary>

| № | Менеджер | Описание |
|---|----------|----------|
| 20 | `ZoneManager` | Игровые зоны |
| 21 | `WorldManager` | Мировые объекты + async HeightMaps |
| 22 | `QuestManager` | Загрузка квестов |
| 23 | `ShipyardManager` | Верфи |

</details>

<details>
<summary><b>Фаза 4 — Игровые системы (27 менеджеров)</b></summary>

| № | Менеджер | Описание |
|---|----------|----------|
| 24 | `FormulaManager` | Формулы расчётов |
| 25 | `ExpirienceManager` | Система опыта |
| 26 | `ConfigurationManager` | Игровые конфигурации |
| 27 | `TlIdManager` | Временные ID |
| 28 | `SpecialtyManager` | Специальные товары |
| 29 | `ItemManager` | Предметы (Load + LoadUserItems) |
| 30 | `AnimationManager` | Анимации |
| 31 | `PlotManager` | Сюжетные скрипты |
| 32 | `SkillManager` | Навыки/умения |
| 33 | `CraftManager` | Крафт/ремесло |
| 34 | `MateManager` | Спутники/маунты |
| 35 | `SlaveManager` | Транспорт/питомцы |
| 36 | `TeamManager` | Группы/рейды |
| 37 | `AuctionManager` | Аукцион |
| 38 | `MailManager` | Почтовая система |
| 39 | `NameManager` | Управление именами |
| 40 | `FactionManager` | Фракции |
| 41 | `ExpeditionManager` | Экспедиции/гильдии |
| 42 | `CharacterManager` | Персонажи |
| 43 | `FamilyManager` | Семьи |
| 44 | `PortalManager` | Порталы |
| 45 | `FriendMananger` | Друзья |
| 46 | `NpcManager` | NPC |
| 47 | `DoodadManager` | Интерактивные объекты |
| 48 | `HousingManager` | Жилищная система |
| 49 | `TransferManager` | Транспортные маршруты |
| 50 | `GimmickManager` | Игровые механики |

</details>

<details>
<summary><b>Фаза 5 — Спавн объектов (3 операции)</b></summary>

| № | Операция | Описание |
|---|----------|----------|
| 51 | `SpawnManager.Load()` | Загрузка точек спавна |
| 52 | `SpawnManager.SpawnAll()` | Создание всех NPC/объектов |
| 53 | `HousingManager.SpawnAll()` | Создание всех строений |

</details>

<details>
<summary><b>Фаза 6 — Сервисы (12 менеджеров)</b></summary>

| № | Менеджер | Описание |
|---|----------|----------|
| 54 | `AccessLevelManager` | Уровни доступа GM |
| 55 | `CashShopManager` | Игровой магазин |
| 56 | `ScriptCompiler` | Компиляция C#-скриптов |
| 57 | `SaveManager` | Автосохранение данных |
| 58 | `SpecialtyManager` | Инициализация специальностей |
| 59 | `BoatPhysicsManager` | Физика кораблей |
| 60 | `TransferManager` | Инициализация трансферов |
| 61 | `GimmickManager` | Инициализация гиммиков |
| 62 | `SlaveManager` | Инициализация транспорта |
| 63 | `EncryptionManager` | Шифрование пакетов |
| 64 | `TimeManager` | Игровое время |
| 65 | `TaskManager` | Запуск планировщика задач |

</details>

<details>
<summary><b>Фаза 7 — Сеть (3 сервиса)</b></summary>

| № | Сервис | Порт | Описание |
|---|--------|------|----------|
| 66 | `GameNetwork` | 1239 | Основная игровая сеть |
| 67 | `StreamNetwork` | 1250 | Потоковая сеть |
| 68 | `LoginNetwork` | 1234 | Связь с Login Server |

</details>

---

## 🎮 Структура игровых моделей

Все игровые модели расположены в `AAEmu.Game/Models/Game/`:

```
Models/Game/
│
├── 📄 ICommand.cs              # Интерфейс команд
├── 📄 AccessLevel.cs           # Уровни доступа
├── 📄 Configurations.cs        # Конфигурации
├── 📄 Portal.cs                # Порталы
├── 📄 Family.cs                # Семьи
├── 📄 Friend.cs                # Друзья
├── 📄 Member.cs                # Участники группы
│
├── 📁 AI/                      # 🤖 Искусственный интеллект NPC
│   └── Поведение, состояния, дерево решений
│
├── 📁 Animation/               # 🎬 Анимации
│   └── Длительности, шаблоны анимаций
│
├── 📁 Auction/                 # 🏪 Аукцион
│   └── Лоты, ставки, торги
│
├── 📁 CashShop/                # 💰 Игровой магазин
│   └── Товары за реальные деньги
│
├── 📁 Char/                    # 👤 Персонаж (самая обширная)
│   ├── Character.cs            # Главная модель персонажа
│   ├── CharacterAbilities.cs   # Способности
│   ├── CharacterSkills.cs      # Умения
│   ├── CharacterQuests.cs      # Квесты персонажа
│   ├── CharacterMails.cs       # Почта
│   ├── CharacterMates.cs       # Спутники
│   ├── CharacterFriends.cs     # Друзья
│   └── ... (15+ файлов)
│
├── 📁 Chat/                    # 💬 Система чата
│   └── Каналы, сообщения, фильтры
│
├── 📁 Crafts/                  # 🔨 Крафт
│   └── Рецепты, очереди крафта
│
├── 📁 DoodadObj/               # 🎯 Интерактивные объекты (Doodads)
│   └── Деревья, шахты, механизмы
│
├── 📁 Expeditions/             # ⚔️ Экспедиции (гильдии)
│   └── Управление гильдиями, ранги
│
├── 📁 Faction/                 # 🏴 Фракции
│   └── Отношения между фракциями
│
├── 📁 Formulas/                # 📊 Формулы расчётов
│   └── Урон, защита, характеристики
│
├── 📁 Gimmicks/                # ⚡ Игровые механики
│   └── Специальные механизмы мира
│
├── 📁 Housing/                 # 🏠 Жилищная система
│   └── Строительство, земельные участки
│
├── 📁 Items/                   # 🎒 Предметы
│   └── Типы, шаблоны, модификаторы
│
├── 📁 Mails/                   # ✉️ Почтовая система
│   └── Письма, вложения
│
├── 📁 Mate/                    # 🐎 Спутники и маунты
│   └── Управление питомцами
│
├── 📁 Merchant/                # 🛒 Торговцы NPC
│   └── Ассортимент, цены
│
├── 📁 NPChar/                  # 👾 NPC
│   └── Шаблоны, поведение, параметры
│
├── 📁 OpenPortal/              # 🌀 Открытые порталы
│   └── Телепортация по миру
│
├── 📁 Quests/                  # 📜 Квесты
│   └── Задания, условия, награды
│
├── 📁 Shipyard/                # ⚓ Верфи
│   └── Строительство кораблей
│
├── 📁 Skills/                  # ✨ Навыки и умения
│   └── Деревья навыков, эффекты, баффы
│
├── 📁 Slaves/                  # 🚗 Транспорт (Slaves)
│   └── Повозки, корабли, глайдеры
│
├── 📁 Team/                    # 👥 Группы и рейды
│   └── Формирование групп, распределение лута
│
├── 📁 Trading/                 # 📦 Торговые маршруты
│   └── Торговые пакеты, маршруты, цены
│
├── 📁 Transfers/               # 🚌 Транспортная система
│   └── Маршруты NPC-транспорта
│
├── 📁 Units/                   # 🎯 Базовые классы юнитов
│   ├── BaseUnit.cs             # Базовый юнит
│   ├── Unit.cs                 # Расширенный юнит
│   └── Route/                  # Система маршрутов
│
└── 📁 World/                   # 🌍 Игровой мир
    └── Зоны, регионы, навигация
```

### Иерархия основных классов

```
BaseUnit
  └── Unit
        ├── Character           # Игровой персонаж
        ├── Npc                 # Неигровой персонаж
        ├── Transfer            # Транспортный NPC
        ├── Slave               # Транспортное средство
        ├── Gimmick             # Механизм
        ├── Mate                # Спутник/маунт
        └── Housing             # Жилое строение
```

---

## 🎮 Система команд

### Интерфейс ICommand

Все GM-команды реализуют интерфейс `ICommand` (`Models/Game/ICommand.cs`):

```csharp
public interface ICommand
{
    void Execute(Character character, string[] args);
    string GetCommandLineHelp();
    string GetCommandHelpText();
}
```

### Архитектура команд

```
┌──────────────────┐     ┌─────────────────┐     ┌──────────────────┐
│  Ввод команды    │────►│  ScriptCompiler  │────►│  CommandManager  │
│  в игровом чате  │     │  (компиляция)    │     │  (маршрутизация) │
│  /команда аргс   │     └─────────────────┘     └────────┬─────────┘
└──────────────────┘                                      │
                                                          ▼
                                              ┌───────────────────────┐
                                              │  AccessLevelManager   │
                                              │  (проверка прав)      │
                                              └───────────┬───────────┘
                                                          │
                                                          ▼
                                              ┌───────────────────────┐
                                              │  ICommand.Execute()   │
                                              │  (выполнение команды) │
                                              └───────────────────────┘
```

**Расположение скриптов:** `AAEmu.Game/Scripts/Commands/`

Команды компилируются динамически через `ScriptCompiler` при старте сервера,
что позволяет добавлять новые команды без перекомпиляции всего проекта.

### Доступные GM-команды (49+)

| Категория | Команды |
|-----------|---------|
| **Телепортация** | `Teleport`, `Move`, `MoveTo`, `MoveAll` |
| **Предметы** | `AddItem`, `ShowInventory`, `Kit` |
| **Валюта** | `AddGold`, `AddLabor` |
| **Персонаж** | `AddXP`, `ChangeLevel`, `Revive`, `Kill`, `Position`, `Height`, `Rotate`, `Invisible` |
| **Бой** | `TestCombat`, `Heal`, `ChallengeDuel`, `StartDuel` |
| **Спавн** | `Spawn`, `Despawn`, `SpawnGrid` |
| **Объекты** | `TickDoodad`, `TestSlave` |
| **Жильё** | `HouseBindingMove` |
| **Чат/Группа** | `SoloParty`, `TestChatChannel`, `Announce` |
| **Конфигурация** | `ReloadConfigs`, `ReloadAuction` |
| **Полёт/Погода** | `Fly`, `Snow` |
| **Тестирование** | `TestEcho`, `TestTransfer`, `TestHeight`, `TestZoneState`, `TestGuild` |
| **Прочее** | `Around`, `Online`, `Nloc`, `Nwrite`, `PingPosition`, `SendPacket`, `Help`, `Scripts`, `Appellation`, `AddPortals` |

---

## 🗄️ Архитектура базы данных

### Схема баз данных

```
┌─────────────────────────────────┐    ┌──────────────────────────────────────┐
│       MySQL: aaemu_login        │    │         MySQL: aaemu_game            │
│─────────────────────────────────│    │──────────────────────────────────────│
│                                 │    │                                      │
│  accounts                       │    │  characters                          │
│  ├── id (PK)                    │    │  ├── id (PK)                         │
│  ├── username                   │    │  ├── account_id → aaemu_login        │
│  ├── password                   │    │  ├── name, race, gender              │
│  └── access_level               │    │  ├── level, experience               │
│                                 │    │  ├── zone_id, x, y, z               │
│  game_servers                   │    │  └── hp, mp, labor, money            │
│  ├── id (PK)                    │    │                                      │
│  ├── name                       │    │  items                               │
│  ├── host, port                 │    │  ├── id (PK)                         │
│  └── active                     │    │  ├── owner_id → characters           │
│                                 │    │  ├── template_id                     │
│                                 │    │  └── slot, count, grade              │
│                                 │    │                                      │
│                                 │    │  housings, quests, mails,            │
│                                 │    │  skills, mates, expeditions ...      │
└─────────────────────────────────┘    └──────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────────┐
│                    SQLite: compact.sqlite3 (только чтение)                  │
│──────────────────────────────────────────────────────────────────────────────│
│  Статические игровые данные:                                                │
│  • Шаблоны навыков (skills)      • Шаблоны предметов (items)               │
│  • Шаблоны NPC (npcs)            • Шаблоны квестов (quests)                │
│  • Данные зон (zones)            • Рецепты крафта (crafts)                 │
│  • Формулы расчётов              • Таблицы опыта                           │
│  • Фракции и отношения           • Торговые маршруты                       │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Система автообновления БД

SQL-скрипты для обновления базы данных хранятся в директории `SQL/`:

```
SQL/
├── aaemu_login/               # Скрипты для БД авторизации
│   ├── create_tables.sql      # Создание начальных таблиц
│   └── updates/               # Инкрементальные обновления
│
└── aaemu_game/                # Скрипты для игровой БД
    ├── create_tables.sql      # Создание начальных таблиц
    └── updates/               # Инкрементальные обновления
```

Система проверяет директорию `sql/` на наличие ожидающих обновлений при старте.

---

## 📚 Зависимости (NuGet)

### AAEmu.Commons — Общая библиотека

| Пакет | Версия | Назначение |
|-------|--------|------------|
| `MySql.Data` | 8.0.27 | Драйвер MySQL для подключения к БД |
| `Newtonsoft.Json` | 13.0.1 | Сериализация/десериализация JSON |
| `NLog` | 5.0.0-preview.3 | Фреймворк логирования |
| `NetCoreServer` | 5.1.0 | Высокопроизводительный TCP/UDP сервер |

### AAEmu.Game — Игровой сервер

| Пакет | Версия | Назначение |
|-------|--------|------------|
| `Jace` | 1.0.0 | Движок вычисления формул (damage calc) |
| `JitterPhysics` | 0.2.0.20 | Физический движок (корабли, транспорт) |
| `Microsoft.CodeAnalysis.CSharp.Scripting` | 4.1.0-1.final | Динамическая компиляция C#-скриптов |
| `Microsoft.Data.Sqlite` | 6.0.0 | SQLite для чтения compact.sqlite3 |
| `Microsoft.Extensions.Configuration` | 6.0.0 | Конфигурация приложения |
| `Microsoft.Extensions.Configuration.Binder` | 6.0.0 | Привязка конфигурации к моделям |
| `Microsoft.Extensions.Configuration.CommandLine` | 6.0.0 | Параметры командной строки |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 6.0.0 | Переменные окружения |
| `Microsoft.Extensions.Configuration.Json` | 6.0.0 | JSON-конфигурация |
| `Microsoft.Extensions.DependencyInjection` | 6.0.0 | Внедрение зависимостей |
| `Microsoft.Extensions.Hosting` | 6.0.0 | Хостинг IHostedService |
| `Ionic.Zlib` | 1.9.1.5 | Сжатие данных |
| `NLog` | 5.0.0-preview.3 | Логирование |
| `NLua` | 1.6.0 | Lua-скриптинг (расширение логики) |
| `Quartz` | 3.3.3 | Планировщик задач (автосохранение и пр.) |

### AAEmu.Login — Сервер авторизации

| Пакет | Версия | Назначение |
|-------|--------|------------|
| `Microsoft.Extensions.Configuration` | 6.0.0 | Конфигурация |
| `Microsoft.Extensions.Configuration.Binder` | 6.0.0 | Привязка конфигурации |
| `Microsoft.Extensions.Configuration.CommandLine` | 6.0.0 | Параметры командной строки |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 6.0.0 | Переменные окружения |
| `Microsoft.Extensions.Configuration.Json` | 6.0.0 | JSON-конфигурация |
| `Microsoft.Extensions.Hosting` | 6.0.0 | Хостинг |
| `NLog` | 5.0.0-preview.3 | Логирование |
| `System.Runtime.Loader` | 4.3.0 | Загрузка сборок |

---

## 📝 Система логирования

### Конфигурация NLog

Оба сервера используют идентичную конфигурацию `NLog.config`:

```
┌─────────────────────────────────────────────────────────────────┐
│                       NLog Pipeline                             │
│                    (autoReload: true)                            │
│─────────────────────────────────────────────────────────────────│
│                                                                 │
│  Источник       Фильтр              Цель                       │
│  ─────────      ──────              ────                        │
│                                                                 │
│  Quartz.*  ──► Trace..Info ──► /dev/null (подавлено, final)    │
│                                                                 │
│  *         ──► Debug+      ──► 🖥️  Консоль (цветная)           │
│            ──► Error+      ──► 📄 Logs/Error.log               │
│            ──► Debug..Warn ──► 📄 Logs/Server.log              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Параметры логирования

| Параметр | Значение |
|----------|----------|
| **Режим** | Асинхронный (async targets) |
| **Консоль** | Цветной вывод (ColoredConsole) |
| **Файл Server.log** | Debug — Warn, ежедневная ротация |
| **Файл Error.log** | Error и выше, ежедневная ротация |
| **Макс. архивов** | 9 файлов на каждый лог |
| **Формат архива** | `Server.yyyy-MM-dd.log`, `Error.yyyy-MM-dd.log` |
| **Кодировка** | UTF-8 |
| **Формат строки** | `HH:mm:ss [LEVEL] LoggerName - Message Exception` |

### Пример вывода в консоль

```
14:23:45 [INFO] GameService - Loading ZoneManager...
14:23:45 [DEBUG] ZoneManager - Loaded 127 zones
14:23:46 [INFO] GameService - Loading WorldManager...
14:23:47 [WARN] WorldManager - Heightmap not found for zone 301
14:23:48 [ERROR] NpcManager - Failed to load NPC template 12345
```

---

## 📋 Конфигурационные файлы

### Основные конфигурации

```
Сервер/
├── Config.json                        # Главная конфигурация (создаётся из ExampleConfig.json)
├── AccessLevels.json                  # Уровни доступа GM-команд
├── NLog.config                        # Настройки логирования
│
├── Data/
│   ├── Configurations.json            # Игровые конфигурации
│   ├── CharTemplates.json             # Шаблоны создания персонажей
│   ├── worlds.json                    # Определения игровых миров
│   ├── housing_bindings.json          # Привязки жилищной системы
│   ├── slave_attach_points.json       # Точки крепления маунтов/транспорта
│   ├── npc_ai_params.xml              # Параметры AI для NPC
│   └── anim_durations.json            # Длительности анимаций
│
└── Data/
    └── compact.sqlite3                # SQLite БД игровых данных (только чтение)
                                       # Содержит: навыки, предметы, NPC, квесты и пр.
```

### Структура Config.json (Game Server)

```json
{
    "Id": 1,
    "AdditionalesId": 1,
    "Key": "секретный_ключ",
    "Network": {
        "Host": "127.0.0.1",
        "Port": 1239
    },
    "StreamNetwork": {
        "Host": "127.0.0.1",
        "Port": 1250
    },
    "LoginNetwork": {
        "Host": "127.0.0.1",
        "Port": 1234
    },
    "Connections": {
        "MySQLProvider": {
            "Host": "localhost",
            "Port": "3306",
            "User": "root",
            "Password": "",
            "Database": "aaemu_game"
        },
        "SQLiteProvider": {
            "Database": "Data/compact.sqlite3"
        }
    },
    "CharacterNameRegex": "^[a-zA-Z]{1,18}$",
    "HeightMapsEnable": true
}
```

### Структура Config.json (Login Server)

```json
{
    "AutoAccount": true,
    "InternalNetwork": {
        "Host": "127.0.0.1",
        "Port": 1234
    },
    "Network": {
        "Host": "*",
        "Port": 1237,
        "NumConnections": 10
    },
    "Connections": {
        "MySQLProvider": {
            "Host": "localhost",
            "Port": "3306",
            "User": "root",
            "Password": "",
            "Database": "aaemu_login"
        }
    }
}
```

> **`AutoAccount: true`** — при включённом режиме сервер автоматически создаёт
> новый аккаунт при первом входе с неизвестным именем пользователя.

---

## 🔐 Поток аутентификации

### Полная схема процесса аутентификации

```
    КЛИЕНТ                   LOGIN SERVER                  GAME SERVER
      │                          │                              │
      │  1. TCP Connect :1237    │                              │
      │─────────────────────────►│                              │
      │                          │                              │
      │  2. CARequestAuthPacket  │                              │
      │  ┌─────────────────────┐ │                              │
      │  │ pFrom (uint32)      │ │                              │
      │  │ pTo (uint32)        │ │                              │
      │  │ svc (byte)          │─┤                              │
      │  │ dev (bool)          │ │                              │
      │  │ account (string)    │ │                              │
      │  │ mac, mac2 (bytes)   │ │                              │
      │  │ cpu (uint64)        │ │                              │
      │  └─────────────────────┘ │                              │
      │                          │                              │
      │                          │  3. LoginController.Login()  │
      │                          │  ┌──────────────────────┐    │
      │                          │  │ Запрос к aaemu_login │    │
      │                          │  │ SELECT * FROM        │    │
      │                          │  │   accounts           │    │
      │                          │  │ WHERE username = ?   │    │
      │                          │  └──────────────────────┘    │
      │                          │                              │
      │                          │  4. AutoAccount?             │
      │                          │  ┌──────────────────────┐    │
      │                          │  │ Если включён и       │    │
      │                          │  │ аккаунт не найден:   │    │
      │                          │  │ INSERT INTO accounts  │    │
      │                          │  └──────────────────────┘    │
      │                          │                              │
      │  5. ACAuthResponsePacket │                              │
      │  ┌─────────────────────┐ │                              │
      │  │ account_id          │ │                              │
      │◄─│ wsk (session key)   │─┤                              │
      │  │ result_code         │ │                              │
      │  └─────────────────────┘ │                              │
      │                          │                              │
      │  6. CAListWorldPacket    │                              │
      │─────────────────────────►│                              │
      │                          │                              │
      │  7. ACWorldListPacket    │                              │
      │  ┌─────────────────────┐ │                              │
      │◄─│ Список серверов     │─┤                              │
      │  │ (id, name, status)  │ │                              │
      │  └─────────────────────┘ │                              │
      │                          │                              │
      │  8. CAEnterWorldPacket   │                              │
      │  (выбор сервера)         │                              │
      │─────────────────────────►│                              │
      │                          │                              │
      │  9. ACWorldCookiePacket  │  10. LGPlayerEnterPacket     │
      │  ┌─────────────────────┐ │  ┌────────────────────────┐  │
      │◄─│ cookie (сессия)     │─┤──│ account_id, cookie     │─►│
      │  │ game_host, game_port│ │  │ wsk (session key)      │  │
      │  └─────────────────────┘ │  └────────────────────────┘  │
      │                          │                              │
      │  11. TCP Connect :1239   │                              │
      │──────────────────────────────────────────────────────── ►│
      │                          │                              │
      │  12. Аутентификация      │                              │
      │  через cookie + wsk      │                              │
      │──────────────────────────────────────────────────────── ►│
      │                          │                              │
      │  13. Вход в игровой мир  │                              │
      │◄────────────────────────────────────────────────────────│
      │                          │                              │
```

### Описание этапов

| Этап | Описание |
|------|----------|
| **1** | Клиент устанавливает TCP-соединение с Login Server на порту 1237 |
| **2** | Клиент отправляет `CARequestAuthPacket` (ID: 0x01) с именем аккаунта и данными клиента |
| **3** | Login Server запрашивает данные аккаунта из таблицы `accounts` в БД `aaemu_login` |
| **4** | Если `AutoAccount = true` и аккаунт не найден, создаётся новый аккаунт автоматически |
| **5** | Login Server отвечает `ACAuthResponsePacket` с ID аккаунта и ключом сессии (WSK) |
| **6** | Клиент запрашивает список доступных игровых миров (`CAListWorldPacket`, ID: 0x0C) |
| **7** | Login Server отправляет `ACWorldListPacket` со списком зарегистрированных Game Server'ов |
| **8** | Клиент выбирает мир и отправляет `CAEnterWorldPacket` (ID: 0x0D) |
| **9** | Login Server генерирует cookie и отправляет клиенту адрес Game Server'а |
| **10** | Login Server передаёт данные аутентификации Game Server'у через внутреннюю сеть (:1234) |
| **11** | Клиент подключается напрямую к Game Server на порту 1239 |
| **12** | Game Server проверяет cookie и WSK-ключ, полученные от Login Server'а |
| **13** | При успешной проверке — клиент входит в игровой мир |

### Поддерживаемые методы аутентификации

| ID пакета | Класс | Регион |
|-----------|-------|--------|
| 0x01 | `CARequestAuthPacket` | Основная аутентификация |
| 0x02 | `CARequestAuthTencentPacket` | Tencent (Китай) |
| 0x03 | `CARequestAuthGameOnPacket` | GameOn (Япония) |
| 0x04 | `CARequestAuthTrionPacket` | Trion Worlds (NA/EU) |
| 0x11 | `CARequestAuthTWPacket` | Тайвань |

---

## 📐 Дополнительные архитектурные заметки

### Физический движок

Для симуляции физики кораблей и транспорта используется **JitterPhysics**,
управляемый через `BoatPhysicsManager`. Это позволяет реалистично обрабатывать
столкновения и движение водного транспорта.

### Скриптовая система

Эмулятор поддерживает два скриптовых движка:

- **C# Scripting** (`Microsoft.CodeAnalysis.CSharp.Scripting`) — динамическая
  компиляция GM-команд и расширений через `ScriptCompiler`
- **Lua** (`NLua`) — скриптинг игровой логики для быстрого прототипирования

### Планировщик задач

**Quartz.NET** используется для:
- Автоматического сохранения данных (`SaveManager`)
- Регенерации очков работы (`LaborPowerManager`)
- Периодических игровых событий
- Управления игровым временем (`TimeManager`)

### Поддерживаемые платформы

Сборка поддерживает следующие Runtime Identifiers:
- `win-x64`, `win-x86` — Windows
- `linux-x64`, `linux-arm`, `linux-arm64` — Linux
- `linux-musl-x64`, `linux-musl-arm`, `linux-musl-arm64` — Alpine Linux

---

> 📅 **Документация актуальна для версии 3.0.3.0-alpha**  
> 📁 **Файл:** `doc/ARCHITECTURE.md`
