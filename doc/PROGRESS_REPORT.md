# Отчёт о состоянии проекта AAEmu 3.0

**Дата:** 19 апреля 2026  
**Ветка:** `copilot/aaemu-3030-compat-fixes`  
**Целевой клиент:** ArcheAge 3.0.3.0 (ArcheRage 3.0.3 RU + EU)  
**Платформа:** .NET 6.0 / C#  
**Целевая система:** Windows 10/11 + WSL2 (Ubuntu)  
**Статус сборки:** ✅ **0 ошибок**, 40 предупреждений (все косметические, pre-existing)

---

## 1. Общая статистика проекта

| Метрика | Значение |
|---------|----------|
| Файлов `.cs` | 1 558 |
| Строк кода C# | ~100 000 |
| Проекты | 3 (Commons, Login, Game) |
| Менеджеры (Singleton) | 51 |
| CS-пакеты (client→server) | 263 из 418 объявленных опкодов |
| SC-пакеты (server→client) | 348 из 761 объявленных опкодов |
| Зарегистрировано пакетов | 369 |
| Закомментировано пакетов | 164 |
| **Пакеты-заглушки** (только логируют) | **140 из 263 (53%)** |
| SQL-схемы | 4 файла |
| Документация | 5 MD-файлов + 14 изображений |
| Клиент (game files) | 33 ГБ |

---

## 2. Что реализовано в текущей сессии

### 2.1. Реверс-инжиниринг клиента

Полный анализ бинарных файлов клиента ArcheAge 3.0.3.0:

| Действие | Результат |
|----------|-----------|
| PE-анализ всех DLL | Экспорт/импорт таблицы для crynetwork.dll, crygame.dll, cryaction.dll, crysystem.dll, xlcommon.dll, xlpack.dll |
| Анализ строковых данных | 2300+ строк протокола и игровых систем из бинарников |
| xlcommon.dll | 567 экспортов: Rijndael/AES (12), CWhirlpoolHash (8), Crc32Gen (6), CityHash, MurmurHash, SHA, MD5 |
| xlpack.dll | 60 экспортов: CreateFileSystem, Mount, FOpen/FRead/FSeek/FClose, AES-CBC/ECB шифрование |
| Крипто-стек | RSA-1024 → AES-CBC-128 + XOR (магические константы обфусцированы) |
| game_pak | 34.78 ГБ, проприетарный формат (не ZIP), шифрование AES-CBC/CFB128/ECB |
| CVar-таблица | cl_serveraddr, cl_serverport, cl_account_id, cl_password, cl_zone_id, cl_world_cookie и др. |
| Менеджеры клиента | Идентифицированы: Auctioneer, CofferManager, HeroManager, Mailman, ReputationManager |

**Создан документ:** `doc/CLIENT_RE_ANALYSIS.md` (36 КБ, 11 разделов)

### 2.2. CofferManager (система сундуков/хранилищ)

Ранее: 3 CS-пакета были заглушками (только чтение данных + лог), 2 SC-пакета отсутствовали, DoodadFuncCoffer не работал.

**Реализовано:**

| Файл | Описание |
|------|----------|
| `Core/Managers/CofferManager.cs` | Менеджер сундуков: открытие/закрытие, отслеживание активных пользователей, swap/split предметов, определение ёмкости через DoodadFuncTemplate |
| `Core/Packets/G2C/SCCofferContentsPacket.cs` | SC-пакет отправки содержимого сундука клиенту (опкод 0x019) |
| `Core/Packets/G2C/SCSplitCofferItemResultPacket.cs` | SC-пакет результата разделения предметов (опкод 0x0d8) |
| `Core/Packets/C2G/CSCofferInteractionPacket.cs` | **Доработан:** вызывает `CofferManager.InteractCoffer()` |
| `Core/Packets/C2G/CSSplitCofferItemPacket.cs` | **Доработан:** вызывает `CofferManager.SplitCofferItem()` |
| `Core/Packets/C2G/CSSwapCofferItemsPacket.cs` | **Доработан:** вызывает `CofferManager.SwapCofferItems()` |
| `Models/Game/DoodadObj/Funcs/DoodadFuncCoffer.cs` | **Доработан:** открывает UI сундука через CofferManager |
| `GameService.cs` | **Доработан:** регистрация CofferManager через SafeLoad |

Функциональность:
- ✅ Открытие/закрытие сундука с проверкой занятости другим игроком
- ✅ Отправка содержимого сундука клиенту
- ✅ Перемещение предметов между инвентарём и сундуком
- ✅ Разделение стаков предметов в сундуке
- ✅ Определение ёмкости из DoodadFuncCoffer шаблона
- ✅ Очистка при отключении игрока / уничтожении дудада

### 2.3. HeroManager (система героев фракций)

Ранее: 5 CS-опкодов и 13 SC-опкодов были объявлены, но не существовало ни одного файла реализации. Все 5 регистраций были закомментированы.

**Реализовано:**

| Файл | Описание |
|------|----------|
| `Core/Managers/HeroManager.cs` | Менеджер героев: сезоны, кандидаты, голосование, рейтинги, 3 фракции |
| `Core/Packets/C2G/CSHeroRankingListPacket.cs` | Запрос рейтинга героев (опкод 0x06a) |
| `Core/Packets/C2G/CSHeroCandidateListPacket.cs` | Запрос списка кандидатов (опкод 0x02f) |
| `Core/Packets/C2G/CSHeroAbstainPacket.cs` | Отказ от кандидатуры (опкод 0x189) |
| `Core/Packets/C2G/CSHeroVotingPacket.cs` | Голосование за кандидата (опкод 0x0d6) |
| `Core/Packets/C2G/CSHeroRequestRankDataPacket.cs` | Запрос данных рейтинга (опкод 0x05a) |
| `Core/Packets/G2C/SCHeroSeasonInfoPacket.cs` | Информация о сезоне (опкод 0x011) |
| `Core/Packets/G2C/SCHeroRankingListPacket.cs` | Список рейтинга героев (опкод 0x0cf) |
| `Core/Packets/G2C/SCHeroCandidateListPacket.cs` | Список кандидатов (опкод 0x0db) |
| `Core/Packets/G2C/SCHeroVotingPacket.cs` | Подтверждение голосования (опкод 0x009) |
| `GameNetwork.cs` | **Доработан:** раскомментированы 5 регистраций Hero-пакетов |
| `GameService.cs` | **Доработан:** регистрация HeroManager через SafeLoad |

Функциональность:
- ✅ Инициализация сезонов героев (Nuia/Haranya/Pirate)
- ✅ Отправка сезонной информации при логине
- ✅ Запрос и отправка рейтинга героев
- ✅ Запрос и отправка списка кандидатов
- ✅ Голосование за кандидата
- ✅ Отказ от кандидатуры

### 2.4. Документация

| Документ | Описание |
|----------|----------|
| `doc/CONTEXT.md` | Технический снимок всего проекта: архитектура, протокол (уровни 1-6), опкоды (419 CS + 762 SC) |
| `doc/EMULATOR_PLAN.md` | 6-фазный план реализации эмулятора |
| `doc/CLIENT_RE_ANALYSIS.md` | Результаты реверс-инжиниринга клиента: DLL, крипто, протокол, CVar, xlcommon API, game_pak |

### 2.5. Среда сборки

- ✅ Установлен .NET 6.0 SDK (v6.0.136) через Homebrew
- ✅ Установлен .NET 10.0 SDK (v10.0.202)
- ✅ Проект собирается: **0 ошибок**
- ✅ Команда сборки: `export DOTNET_ROOT="/opt/homebrew/opt/dotnet@6/libexec" && export PATH="$DOTNET_ROOT:$PATH" && dotnet build AAEmu.sln`

---

## 3. Что ещё нужно сделать

### 3.1. Критически важное (без этого сервер не будет полноценно работать)

#### 3.1.1. Реализация 140 пакетов-заглушек

**Проблема:** 140 из 263 CS-пакетов (53%) только читают данные из потока и логируют, но не вызывают никакой игровой логики.

**Требуется:**
- Для каждого пакета: добавить метод `Execute()`, вызывающий соответствующий менеджер
- Некоторые пакеты требуют создания новых менеджеров (см. ниже)

**Примеры крупных групп заглушек:**

| Система | Кол-во заглушек | Примеры |
|---------|----------------|---------|
| Аукцион | ~8 | CSAuctionPostPacket, CSAuctionSearchPacket, CSBidAuctionPacket, CSCancelAuctionPacket |
| Жильё | ~10 | CSBuyHousePacket, CSSellHousePacket, CSDecorateHousePacket, CSChangeHousePayPacket |
| Суд/тюрьма | ~6 | CSJuryVerdictPacket, CSReplyImprisonOrTrialPacket, CSJoinTrialAudiencePacket |
| Экспедиции | ~5 | CSDismissExpeditionPacket, CSRenameExpeditionPacket, CSChangeExpeditionSponsorPacket |
| Квесты | ~5 | CSQuestStartWithPacket, CSResetQuestContextPacket, CSRestartMainQuestPacket |
| Торговля/NPC | ~8 | CSBuySpecialtyItemPacket, CSSellBackpackGoodsPacket, CSListSpecialtyGoodsPacket |
| Предметы/инвентарь | ~6 | CSItemSecurePacket, CSEquipmentsSecurePacket, CSThisTimeUnpackItemPacket |
| Рейтинги | ~4 | CSRankCharacterPacket, CSRankSnapshotPacket |
| Прочее | ~88 | Телеграфы, ICS, слейвы, трансферы, навигация |

#### 3.1.2. Реализация отсутствующих SC-пакетов

**Проблема:** 761 SC-опкод объявлен, но реализовано только 348 файлов (46%). Недостающие 413 SC-пакетов означают, что клиент не будет получать данные о многих системах.

**Ключевые отсутствующие SC-пакеты Hero-системы (9 шт.):**

| Опкод | Имя | Назначение |
|-------|-----|------------|
| 0x23b | SCHeroSeasonOffPacket | Завершение сезона героев |
| 0x1b7 | SCHeroScoreUpdatedPacket | Обновление очков героя |
| 0x0f9 | SCHeroCandidateNotificationPacket | Уведомление о кандидатуре |
| 0x00e | SCHeroListPacket | Список текущих героев |
| 0x1ab | SCHeroEventStatePacket | Состояние события героев |
| 0x179 | SCHeroElectionMailPacket | Письмо о выборах |
| 0x1bd | SCHeroInfoDeletedPacket | Удаление героя |
| 0x13c | SCHeroInfoUpdatedPacket | Обновление информации о герое |
| 0x263 | SCHeroMobilizationOrderUpdatedPacket | Мобилизационный приказ |

#### 3.1.3. Персистентность данных (БД)

**Проблема:** CofferManager и HeroManager хранят данные только в памяти. При перезапуске сервера всё теряется.

**Требуется:**
- Таблица `coffers` (doodad_id, items JSON или отдельная таблица coffer_items)
- Таблица `heroes` (character_id, faction_id, rank, score, season_id)
- Таблица `hero_votes` (voter_id, candidate_id, season_id)
- ALTER для `doodads` таблицы (связь с coffer)
- Миграционные SQL-скрипты

### 3.2. Важное (существенно улучшит функциональность)

#### 3.2.1. 164 закомментированных регистрации пакетов

Пакеты, для которых файлы реализации существуют, но регистрация закомментирована в GameNetwork.cs. Многие из них могут быть рабочими — требуется проверка и активация.

#### 3.2.2. game_pak — извлечение данных клиента

**Проблема:** Файл game_pak (34.78 ГБ) содержит все ресурсы клиента (модели, текстуры, скрипты, LUA, XML конфигурации), но использует проприетарный формат XLGames с AES-шифрованием.

**Содержимое game_pak критически для:**
- Извлечение server-side данных (таблицы предметов, NPC, квестов)
- Структуры пакетов (LUA-скрипты клиента)
- Шаблоны дудадов, навыков, зон

**На Windows:** xlpack.dll — нативная Win32 DLL, можно загрузить напрямую через P/Invoke или написать утилиту-распаковщик.

#### 3.2.3. SQLite база данных

Проект использует SQLite для game data (шаблоны предметов, NPC, скиллы), но файл `compact.sqlite3` отсутствует в репозитории. Без него многие менеджеры не загрузят данные.

---

## 4. Варианты реализации на Windows + WSL2

### 4.1. Извлечение game_pak (нативно на Windows)

На Windows xlpack.dll загружается нативно — это основное преимущество перед macOS.

| Вариант | Описание | Сложность |
|---------|----------|-----------|
| **C# P/Invoke утилита** | Написать .NET-обёртку над xlpack.dll (32-bit) — `CreateFileSystem`, `Mount`, `FOpen`/`FRead`/`FClose`. Скомпилировать как x86 | Средняя |
| **Существующие распаковщики** | Искать готовые инструменты для ArcheAge game_pak (ArcheAge Tools, paktools) | Низкая |
| **Reverse xlpack** | Написать чистый Python/C# парсер формата | Высокая (но долгосрочно полезнее) |

**Рекомендация:** Начать с поиска готовых распаковщиков. Если не найдутся — написать P/Invoke-обёртку (xlpack.dll → x86 .NET Console App).

### 4.2. Установка среды (Windows)

```powershell
# 1. Установить .NET 6 SDK
winget install Microsoft.DotNet.SDK.6

# 2. Клонировать репо
git clone -b copilot/aaemu-3030-compat-fixes https://github.com/l3nsa/archeage3.0.git
cd archeage3.0

# 3. Собрать
dotnet build AAEmu.sln

# 4. Установить MySQL (вариант A: Windows)
winget install Oracle.MySQL
# или скачать MySQL Installer: https://dev.mysql.com/downloads/installer/

# 5. Создать БД
mysql -u root -p < SQL/aaemu_login.sql
mysql -u root -p < SQL/aaemu_game_full.sql

# 6. Скачать compact.sqlite3 (game data) — нужен внешний источник

# 7. Настроить конфиг
# Отредактировать Configurations/game.json и login.json

# 8. Запустить (два терминала)
dotnet run --project AAEmu.Login
dotnet run --project AAEmu.Game
```

### 4.3. Установка среды (WSL2 Ubuntu)

```bash
# 1. Включить WSL2 (PowerShell от администратора)
wsl --install -d Ubuntu

# 2. Внутри WSL — установить .NET 6
sudo apt update && sudo apt install -y dotnet-sdk-6.0

# 3. Установить MySQL
sudo apt install -y mysql-server
sudo systemctl start mysql
sudo mysql_secure_installation

# 4. Клонировать и собрать
git clone -b copilot/aaemu-3030-compat-fixes https://github.com/l3nsa/archeage3.0.git
cd archeage3.0
dotnet build AAEmu.sln

# 5. Создать БД
sudo mysql < SQL/aaemu_login.sql
sudo mysql < SQL/aaemu_game_full.sql

# 6. Настроить Configurations/game.json
# MySQL host: 127.0.0.1, port: 3306
# SQLite path: путь к compact.sqlite3

# 7. Запустить
dotnet run --project AAEmu.Login &
dotnet run --project AAEmu.Game
```

### 4.4. Гибридная схема (Windows + WSL2)

Оптимальный вариант — совмещать оба окружения:

| Компонент | Где запускать | Почему |
|-----------|---------------|--------|
| **Сборка и запуск сервера** | WSL2 (Ubuntu) | Ближе к prod-окружению, нативный MySQL, быстрее |
| **game_pak распаковка** | Windows (нативно) | xlpack.dll — Win32 x86 DLL |
| **Клиент ArcheAge** | Windows (нативно) | x86 Windows-приложение |
| **MITM-прокси (анализ)** | Windows | Между клиентом и сервером в WSL |
| **VS Code** | Windows + Remote WSL | Редактирование кода в WSL через расширение Remote |
| **MySQL** | WSL2 | `sudo apt install mysql-server`, доступен из Windows через localhost |

### 4.5. Дальнейшая разработка

| Задача | Вариант реализации | Оценка файлов |
|--------|-------------------|---------------|
| Заглушки → логика | Итеративно: по системам (аукцион, жильё, суд и т.д.) | 140 файлов |
| Недостающие SC-пакеты | Создавать по мере реализации менеджеров | ~413 файлов |
| Персистентность Coffer/Hero | SQL-миграции + Load()/Save() в менеджерах | 4-6 файлов |
| game_pak распаковка | Нативно через xlpack.dll на Windows | 1-2 файла утилиты |
| Динамический анализ протокола | Запуск клиента → MITM-прокси → захват пакетов | Windows: клиент + прокси |
| Тестирование пакетов | Unit-тесты для сериализации/десериализации | Новый тест-проект |

---

## 5. Сводка изменений в файлах

### Новые файлы (14):

| # | Файл | Тип |
|---|------|-----|
| 1 | `Core/Managers/CofferManager.cs` | Менеджер |
| 2 | `Core/Managers/HeroManager.cs` | Менеджер |
| 3 | `Core/Packets/G2C/SCCofferContentsPacket.cs` | SC-пакет |
| 4 | `Core/Packets/G2C/SCSplitCofferItemResultPacket.cs` | SC-пакет |
| 5 | `Core/Packets/G2C/SCHeroSeasonInfoPacket.cs` | SC-пакет |
| 6 | `Core/Packets/G2C/SCHeroRankingListPacket.cs` | SC-пакет |
| 7 | `Core/Packets/G2C/SCHeroCandidateListPacket.cs` | SC-пакет |
| 8 | `Core/Packets/G2C/SCHeroVotingPacket.cs` | SC-пакет |
| 9 | `Core/Packets/C2G/CSHeroRankingListPacket.cs` | CS-пакет |
| 10 | `Core/Packets/C2G/CSHeroCandidateListPacket.cs` | CS-пакет |
| 11 | `Core/Packets/C2G/CSHeroAbstainPacket.cs` | CS-пакет |
| 12 | `Core/Packets/C2G/CSHeroVotingPacket.cs` | CS-пакет |
| 13 | `Core/Packets/C2G/CSHeroRequestRankDataPacket.cs` | CS-пакет |
| 14 | `doc/PROGRESS_REPORT.md` | Документация |

### Модифицированные файлы (6):

| # | Файл | Что изменено |
|---|------|-------------|
| 1 | `Core/Packets/C2G/CSCofferInteractionPacket.cs` | Добавлен Execute(), вызов CofferManager |
| 2 | `Core/Packets/C2G/CSSplitCofferItemPacket.cs` | Добавлен Execute(), вызов CofferManager |
| 3 | `Core/Packets/C2G/CSSwapCofferItemsPacket.cs` | Добавлен Execute(), вызов CofferManager |
| 4 | `Models/Game/DoodadObj/Funcs/DoodadFuncCoffer.cs` | Use() теперь открывает сундук |
| 5 | `GameService.cs` | Добавлены SafeLoad для CofferManager и HeroManager |
| 6 | `Core/Network/Game/GameNetwork.cs` | Раскомментированы 5 Hero-регистраций |

### Документы (создана ранее):

| # | Файл | Размер |
|---|------|--------|
| 1 | `doc/CONTEXT.md` | 8.9 КБ |
| 2 | `doc/EMULATOR_PLAN.md` | 26 КБ |
| 3 | `doc/CLIENT_RE_ANALYSIS.md` | 36 КБ |

---

## 6. Приоритеты дальнейшей работы

| # | Задача | Приоритет | Зависимости |
|---|--------|-----------|-------------|
| 1 | MySQL + запуск сервера | 🔴 Высший | `winget install Oracle.MySQL` или `sudo apt install mysql-server` (WSL) |
| 2 | Получить compact.sqlite3 | 🔴 Высший | Внешний источник / извлечение из game_pak (xlpack.dll нативно на Windows) |
| 3 | Персистентность Coffer/Hero | 🔴 Высокий | MySQL (#1) |
| 4 | Аукцион — оживить заглушки | 🟠 Высокий | AuctionManager уже есть |
| 5 | Жильё — оживить заглушки | 🟠 Высокий | HousingManager уже есть |
| 6 | Недостающие 9 Hero SC-пакетов | 🟡 Средний | Нет зависимостей |
| 7 | Суд/тюрьма система | 🟡 Средний | Новый менеджер |
| 8 | game_pak — распаковка через xlpack.dll | 🟡 Средний | Windows нативно (xlpack.dll Win32) |
| 9 | 164 закомментированных пакета | 🟢 Легко | Только проверка + раскомментирование |
| 10 | Протокол MITM-анализ | 🟢 Низкий | Клиент (Windows) + сервер (WSL2) |
