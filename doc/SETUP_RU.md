# Установка и запуск ArcheAge 3.0.3.0 (AAEmu) — сервер на WSL, клиент на Windows

> Репозиторий: [l3nsa/archeage3.0](https://github.com/l3nsa/archeage3.0)
> Целевой клиент игры: **3.0.3.0 (2017-03-15)**
> Платформа сервера: WSL2 Ubuntu 24.04 + .NET 8 + MariaDB 10.11
> Платформа клиента: Windows 10 / 11

---

## 1. Что уже сделано в этом рабочем пространстве

- Установлены `dotnet-sdk-8.0`, `mariadb-server`, `mariadb-client`.
- Создан пароль `root/root` для MariaDB, созданы базы `aaemu_login` и `aaemu_game`.
- Загружены дампы `SQL/aaemu_login.sql` и `SQL/aaemu_game_full.sql`.
- Установлена дефолтная кодировка MariaDB `utf8mb4_general_ci` (`/etc/mysql/mariadb.conf.d/99-aaemu.cnf`), чтобы старый драйвер MySQL мог прочитать метаданные.
- Обновлён пакет `MySql.Data` с `8.0.27` до `8.4.0` (иначе `KeyNotFoundException: 24929` на MariaDB ≥10.6).
- `AAEmu.Commons/Utils/CliUtil.cs` переписан для кроссплатформенной работы — убраны обращения к `Console.Title` и `WindowsIdentity` на Linux.
- Создан `/etc/mysql/mariadb.conf.d/99-aaemu.cnf`.
- Созданы файлы `Config.json` для обоих серверов в `bin/Release/net6.0/`, с `Host = "*"` чтобы порты были доступны из Windows-хоста.
- Созданы скрипты `run_login.sh` и `run_game.sh` в корне репозитория.
- **Login-сервер проверен в работе** — запускается, подключается к БД, слушает порты 1234 (Internal) и 1237 (External).
- **Game-сервер НЕ запускается** — не хватает файла `Data/compact.sqlite3` (см. раздел 4).

---

## 2. Проверка и запуск сервера (WSL)

```bash
# Проверить что MariaDB запущена
sudo service mariadb status

# Запустить Login-сервер (один терминал)
cd ~/archeage3.0
./run_login.sh

# Запустить Game-сервер (другой терминал) — ТРЕБУЕТ compact.sqlite3
./run_game.sh
```

Скрипты автоматически:
- Копируют `Config.json` из `ExampleConfig.json`, если его нет.
- Создают папку `sql/` для миграций.
- Ставят `DOTNET_ROLL_FORWARD=LatestMajor`, чтобы .NET 8 мог запускать сборку под net6.0.

Порты, которые слушает сервер:

| Порт | Назначение | Описание |
|---|---|---|
| 1234 | Login ↔ Game | Внутренний канал между серверами |
| 1237 | Login ← Клиент | Авторизация (клиент игры) |
| 1239 | Game ← Клиент | Игровой трафик |
| 1250 | Game ← Клиент | Поток данных (streaming) |
| 3306 | MariaDB | База данных |

В WSL2 порты, слушающие `0.0.0.0`/`*`, автоматически доступны из Windows через `localhost`. Дополнительная настройка `netsh` не требуется для локальной игры.

---

## 3. Блокер: Data/compact.sqlite3

**Game-сервер требует файл `Data/compact.sqlite3`** — это SQLite-база с игровыми данными (таблицы `item_templates`, `npc_templates`, `skills`, `doodad_templates` и т. д.), извлечёнными из игрового клиента 3.0.3.0.

Этот файл **не распространяется в репозитории** по лицензионным причинам (содержит данные XLGames).

### Где его взять

Обычно `compact.sqlite3` входит в раздачу клиента ArcheAge 3.0.3.0 из общей ссылки, приведённой в `readme.md`:

> https://mega.nz/#F!C3Q0WQjT!vRUethZLPiYSo2B4nE_etg!fyIykIZC

В этих раздачах он лежит рядом с клиентом либо идёт отдельным архивом (`compact.sqlite3.zip`, размер 100–400 МБ).

Если там его нет — нужно самому извлечь `db_client.sqlite` / `compact.sqlite3` из клиентского `game_pak` утилитой вроде `KrunchPak` или AAPak (AAEmu/AAPak на GitHub) и преобразовать его в нужный формат.

### Куда положить

```
/home/l3nsa/archeage3.0/AAEmu.Game/bin/Release/net6.0/Data/compact.sqlite3
```

После этого `./run_game.sh` должен загрузиться до стадии ожидания клиента.

---

## 4. Установка клиента на Windows

### 4.1. Получить игровой клиент 3.0.3.0

Скачайте клиент по ссылке из `readme.md` (Mega.nz). Обычно это ZIP-архив на ~20 ГБ, после распаковки структура примерно такая:

```
C:\Games\ArcheAge\
├── bin32\
├── bin64\
│   └── archeage.exe
├── CDN\
│   └── ServiceInfo\
│       └── ServiceInfo_Live_*.xml
├── working\
│   └── aaclient.exe
├── aa.cfg
├── Patcher.exe
└── Launcher.exe
```

Патчер и лаунчер запускать **не нужно** — они идут на оригинальные серверы XLGames.

### 4.2. Настройка клиента на подключение к вашему серверу

Найдите файл `aa.cfg` (или в `bin64/aa.cfg`) и отредактируйте:

```ini
loginAddress = 127.0.0.1
loginPort = 1237
```

Если `aa.cfg` отсутствует или клиент упорно идёт на официальный сервер — используйте патчер хостов:

1. Откройте `C:\Windows\System32\drivers\etc\hosts` от имени Администратора.
2. Добавьте строки:
   ```
   127.0.0.1 authgate-aa.trionworlds.com
   127.0.0.1 arch-login-live.trionworlds.com
   ```
   (Список хостов зависит от билда клиента — обычно их видно через `Wireshark` или `Fiddler`.)

### 4.3. Запустить клиент напрямую

1. Перейдите в папку `bin64\` (или `bin32\` для 32-битного клиента).
2. Запустите `archeage.exe` напрямую, минуя лаунчер:
   ```
   archeage.exe +auth_ip 127.0.0.1 +auth_port 1237 +acc test +pwd test
   ```
   Если параметры командной строки не работают — используйте `aa.cfg` (см. 4.2).

### 4.4. Создание аккаунта

При `"AutoAccount": true` в `Config.json` любой логин/пароль, введённый в клиенте, создаёт новый аккаунт автоматически. Достаточно ввести произвольные `login / password` в окне входа.

Первый созданный аккаунт можно вручную пометить GM'ом:

```sql
mysql -uroot -proot aaemu_login -e \
  "UPDATE users SET access_level = 100 WHERE username = 'ваш_логин';"
```

Значения `access_level` смотрите в `AAEmu.Game/AccessLevels.json`.

---

## 5. Диагностика

| Симптом | Причина и решение |
|---|---|
| `You must install or update .NET to run this application. Framework: Microsoft.NETCore.App, version 6.0.0` | Установите `dotnet-sdk-8.0` и запускайте с `DOTNET_ROLL_FORWARD=LatestMajor` (это делают скрипты `run_*.sh`). |
| `KeyNotFoundException: 24929` в логе логин-сервера | Старая версия `MySql.Data`. В этом репо уже обновлено до `8.4.0`. Если откатили — выполните `dotnet build -c Release`. |
| `System.PlatformNotSupportedException: get_Title()` | `CliUtil.cs` уже пропатчен. Убедитесь, что пересобрали после обновления репо. |
| `SQLite Error 14: 'unable to open database file'` в Game-сервере | Нет `Data/compact.sqlite3`. См. раздел 3. |
| Клиент не подключается к `1237` из Windows | Проверьте: в WSL `ss -tnlp \| grep 1237` должен показать слушающий процесс на `*:1237`. Проверьте Windows firewall. |
| Сервер запустился, но в клиенте «Server is not available» | Сервер должен быть зарегистрирован в `aaemu_login.game_servers`: `SELECT * FROM aaemu_login.game_servers;`. Запись должна иметь Id=1, совпадающий с `"Id"` из `AAEmu.Game/Config.json`. |
| `Config.json doesn't exist!` | Скрипт `run_*.sh` копирует из `ExampleConfig.json`. Если вы запускаете `dotnet AAEmu.Login.dll` напрямую — убедитесь, что находитесь в `bin/Release/net6.0/` и `Config.json` есть рядом. |

---

## 6. Известные проблемы и план улучшений

Список доработок и багов — см. [doc/IMPROVEMENT_PLAN.md](doc/IMPROVEMENT_PLAN.md).

Репозиторий [l3nsa/aaemu](https://github.com/l3nsa/aaemu) (форк **AAEmu/AAEmu**) — это **другой сервер под клиент версии 1.2**, а не обновлённая версия этого проекта. Его можно использовать как источник идей для портирования отдельных менеджеров (см. фазы 3–4 в плане улучшений), но «обновить клиент 3.0.3.0 до состояния v1.2» нельзя — это разные версии с разным протоколом.
