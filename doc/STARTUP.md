# 🚀 Руководство по запуску ArcheAge 3.0 Server Emulator

Данное руководство подробно описывает процесс установки, настройки и запуска серверного эмулятора ArcheAge 3.0.3.0. Следуйте инструкциям шаг за шагом.

---

## 📋 Системные требования

| Компонент | Минимальные требования |
|---|---|
| **Операционная система** | Windows 10+ / Linux (Ubuntu 18.04+) |
| **.NET SDK** | 6.0 или новее |
| **База данных** | MySQL 5.7+ или MariaDB 10.3+ |
| **Git** | Любая актуальная версия |
| **Оперативная память** | 4 ГБ и более (рекомендуется) |
| **Свободное место на диске** | 2 ГБ и более |

---

## 📦 Установка

### Шаг 1. Клонирование репозитория

Откройте терминал (или командную строку) и выполните:

```bash
git clone https://github.com/l3nsa/archeage3.0.git
cd archeage3.0
```

После выполнения этих команд у вас появится папка `archeage3.0` со всеми исходными файлами проекта.

---

### Шаг 2. Установка .NET SDK

Сервер написан на C# и требует .NET 6.0 SDK для сборки и запуска.

#### Windows

1. Перейдите на официальный сайт: https://dotnet.microsoft.com/download
2. Скачайте установщик **.NET 6.0 SDK** (или новее) для Windows.
3. Запустите установщик и следуйте инструкциям на экране.
4. После установки откройте командную строку и проверьте:

```bash
dotnet --version
```

Должна отобразиться версия 6.0.x или выше.

#### Linux (Ubuntu / Debian)

Выполните следующие команды в терминале:

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-6.0
```

Проверьте установку:

```bash
dotnet --version
```

> **Примечание:** Для других дистрибутивов Linux обратитесь к [официальной документации Microsoft](https://learn.microsoft.com/ru-ru/dotnet/core/install/linux).

---

### Шаг 3. Установка MySQL / MariaDB

Серверный эмулятор использует MySQL (или MariaDB) для хранения данных об аккаунтах и игровом мире.

#### Windows

1. Скачайте **MySQL Installer** с официального сайта: https://dev.mysql.com/downloads/installer/
2. Запустите установщик и выберите тип установки **Server only** (или **Full**).
3. Во время настройки задайте пароль для пользователя `root` — **запомните его**, он понадобится позже.
4. Убедитесь, что сервер MySQL запущен (через MySQL Workbench, Службы Windows или `services.msc`).

#### Linux (Ubuntu / Debian)

```bash
sudo apt-get update
sudo apt-get install -y mysql-server
sudo mysql_secure_installation
```

При выполнении `mysql_secure_installation`:
- Задайте пароль для пользователя `root`.
- На остальные вопросы можно ответить `Y` (да) для повышения безопасности.

Убедитесь, что MySQL работает:

```bash
sudo systemctl status mysql
```

Если сервис не запущен, запустите его:

```bash
sudo systemctl start mysql
sudo systemctl enable mysql
```

---

### Шаг 4. Настройка базы данных

#### 4.1. Создание баз данных

Подключитесь к MySQL:

```bash
mysql -u root -p
```

Введите пароль, заданный при установке, и выполните следующие SQL-команды:

```sql
CREATE DATABASE aaemu_login CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
CREATE DATABASE aaemu_game CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
```

Выйдите из MySQL:

```sql
EXIT;
```

#### 4.2. Импорт схем базы данных

В папке `SQL/` находятся два файла:
- `aaemu_login.sql` — структура базы данных логин-сервера
- `aaemu_game_full.sql` — структура базы данных игрового сервера

Выполните импорт из корневой папки проекта:

```bash
mysql -u root -p aaemu_login < SQL/aaemu_login.sql
mysql -u root -p aaemu_game < SQL/aaemu_game_full.sql
```

Система запросит пароль перед каждой командой. Введите пароль пользователя `root`.

> **Важно:** Если при импорте возникнут ошибки, убедитесь, что базы данных были созданы на предыдущем шаге.

---

### Шаг 5. Настройка конфигурации серверов

Каждый сервер (логин и игровой) имеет свой файл конфигурации `Config.json`. В репозитории предоставлены шаблоны — `ExampleConfig.json`.

#### 5.1. Копирование файлов конфигурации

##### Windows (командная строка):

```cmd
copy AAEmu.Login\ExampleConfig.json AAEmu.Login\Config.json
copy AAEmu.Game\ExampleConfig.json AAEmu.Game\Config.json
```

##### Linux:

```bash
cp AAEmu.Login/ExampleConfig.json AAEmu.Login/Config.json
cp AAEmu.Game/ExampleConfig.json AAEmu.Game/Config.json
```

#### 5.2. Настройка логин-сервера

Откройте файл `AAEmu.Login/Config.json` в текстовом редакторе и приведите его к следующему виду (подставив свои значения):

```json
{
    "SecretKey": "test",
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
            "Password": "your_password",
            "Database": "aaemu_login"
        }
    }
}
```

**Описание параметров:**

| Параметр | Описание |
|---|---|
| `SecretKey` | Секретный ключ для связи между логин-сервером и игровым сервером. **Должен совпадать** на обоих серверах. |
| `AutoAccount` | Если `true`, аккаунт создаётся автоматически при первом входе. Удобно для тестирования. |
| `InternalNetwork.Host` | Адрес для внутренней связи с игровым сервером. Оставьте `127.0.0.1` для локального запуска. |
| `InternalNetwork.Port` | Порт для внутренней связи. По умолчанию: `1234`. |
| `Network.Host` | Адрес для подключения клиентов. `*` означает все интерфейсы. |
| `Network.Port` | Порт для подключения клиентов. По умолчанию: `1237`. |
| `MySQLProvider.Password` | **Замените `your_password`** на пароль вашего MySQL-пользователя. |

#### 5.3. Настройка игрового сервера

Откройте файл `AAEmu.Game/Config.json` и приведите его к следующему виду:

```json
{
    "Id": 1,
    "AdditionalesId": [],
    "SecretKey": "test",
    "AutoAccount": true,
    "Network": {
        "Host": "127.0.0.1",
        "Port": 1239,
        "NumConnections": 10
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
            "Password": "your_password",
            "Database": "aaemu_game"
        }
    },
    "CharacterNameRegex": "^[a-zA-Z0-9а-яА-Я]{1,18}$",
    "MaxConcurencyThreadPool": 8,
    "HeightMapsEnable": true
}
```

**Описание дополнительных параметров:**

| Параметр | Описание |
|---|---|
| `Id` | Уникальный идентификатор игрового сервера. |
| `Network.Host` | Адрес, на котором игровой сервер принимает подключения от клиентов. |
| `Network.Port` | Порт игрового сервера. По умолчанию: `1239`. |
| `StreamNetwork` | Настройки потокового сервера для загрузки карт. По умолчанию: порт `1250`. |
| `LoginNetwork` | Адрес и порт логин-сервера. **Должен совпадать** с `InternalNetwork` в конфигурации логин-сервера. |
| `CharacterNameRegex` | Регулярное выражение для допустимых имён персонажей. Поддерживает латиницу, кириллицу и цифры (до 18 символов). |
| `MaxConcurencyThreadPool` | Максимальное количество потоков. Увеличьте для более мощных серверов. |
| `HeightMapsEnable` | Включить карты высот для физики/навигации. |
| `MySQLProvider.Password` | **Замените `your_password`** на пароль вашего MySQL-пользователя. |

> **Важно:** Значение `SecretKey` должно быть **одинаковым** в конфигурациях логин-сервера и игрового сервера. Иначе серверы не смогут установить связь друг с другом.

---

### Шаг 6. Сборка проекта

Из корневой папки проекта выполните:

```bash
dotnet restore
dotnet build
```

- `dotnet restore` — загружает все необходимые NuGet-пакеты (зависимости).
- `dotnet build` — компилирует весь проект.

Если сборка завершилась без ошибок, вы увидите сообщение `Build succeeded`. Можно переходить к запуску.

> **Совет:** Если при сборке возникают ошибки, убедитесь, что установлен .NET SDK 6.0 или новее (`dotnet --version`).

---

### Шаг 7. Запуск серверов

Серверы необходимо запускать в определённом порядке: **сначала логин-сервер**, затем **игровой сервер**.

#### Windows: Быстрый запуск (BAT-файлы)

**Оба сервера одновременно:**

Дважды щёлкните по файлу `start_AAEmu.bat` в корне проекта. Откроются два окна консоли — по одному для каждого сервера.

**По отдельности:**

1. Запустите `StartLoginServer.bat` — откроется логин-сервер.
2. Запустите `StartGameServer.bat` — откроется игровой сервер.

#### Linux: Запуск из исходного кода

Вам понадобятся **два терминала** (или используйте `tmux` / `screen`).

**Терминал 1 — Логин-сервер:**

```bash
cd AAEmu.Login
dotnet run
```

**Терминал 2 — Игровой сервер:**

```bash
cd AAEmu.Game
dotnet run
```

> **Совет:** Можно использовать `tmux` для удобного управления терминалами:
> ```bash
> tmux new-session -d -s login 'cd AAEmu.Login && dotnet run'
> tmux new-session -d -s game 'cd AAEmu.Game && dotnet run'
> tmux attach -t game
> ```

#### Запуск из опубликованной (Published) сборки

Для оптимальной производительности рекомендуется использовать опубликованную сборку:

```bash
# Публикация
dotnet publish AAEmu.Login/AAEmu.Login.csproj -c Release
dotnet publish AAEmu.Game/AAEmu.Game.csproj -c Release

# Запуск логин-сервера
dotnet AAEmu.Login/bin/Release/net6.0/publish/AAEmu.Login.dll

# Запуск игрового сервера (в отдельном терминале)
dotnet AAEmu.Game/bin/Release/net6.0/publish/AAEmu.Game.dll
```

> **Примечание:** Опубликованная сборка работает быстрее, так как код предварительно оптимизирован.

---

### Шаг 8. Запуск через Docker (опционально)

> **Примечание:** На данный момент в репозитории нет официальных файлов Docker. Ниже приведён пример конфигурации `docker-compose.yml`, который можно использовать для контейнеризации:

```yaml
version: '3.8'

services:
  mysql:
    image: mysql:5.7
    container_name: aaemu-mysql
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: your_password
      MYSQL_DATABASE: aaemu_login
    ports:
      - "3306:3306"
    volumes:
      - mysql_data:/var/lib/mysql
      - ./SQL/aaemu_login.sql:/docker-entrypoint-initdb.d/01-login.sql
      - ./SQL/aaemu_game_full.sql:/docker-entrypoint-initdb.d/02-game.sql

  login-server:
    build:
      context: .
      dockerfile: Dockerfile
      args:
        PROJECT: AAEmu.Login
    container_name: aaemu-login
    restart: unless-stopped
    depends_on:
      - mysql
    ports:
      - "1234:1234"
      - "1237:1237"
    volumes:
      - ./AAEmu.Login/Config.json:/app/Config.json

  game-server:
    build:
      context: .
      dockerfile: Dockerfile
      args:
        PROJECT: AAEmu.Game
    container_name: aaemu-game
    restart: unless-stopped
    depends_on:
      - mysql
      - login-server
    ports:
      - "1239:1239"
      - "1250:1250"
    volumes:
      - ./AAEmu.Game/Config.json:/app/Config.json
      - ./AAEmu.Game/Data:/app/Data

volumes:
  mysql_data:
```

Для использования Docker создайте также файл `Dockerfile` в корне проекта:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet publish ${PROJECT}/${PROJECT}.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet"]
CMD ["AAEmu.Login.dll"]
```

> **Важно:** При использовании Docker-контейнеров убедитесь, что в `Config.json` адреса `Host` указывают на имена сервисов Docker (например, `mysql` вместо `localhost`), а не на `127.0.0.1`.

---

## ✅ Проверка запуска

После запуска серверов убедитесь, что всё работает корректно:

### Логин-сервер

- В консоли должен отобразиться заголовок **«Login»**.
- Сервер привязывается к порту **1237** (для клиентов) и **1234** (для связи с игровым сервером).
- В логах не должно быть ошибок подключения к MySQL.

### Игровой сервер

- В консоли должен отобразиться заголовок **«Game & Stream»**.
- Сервер последовательно загружает все менеджеры (Manager): загрузка мира, предметов, NPC, навыков и т.д.
- Сервер привязывается к порту **1239** (игровой) и **1250** (потоковый).
- В логах должно появиться сообщение об успешном подключении к базе данных.

### Проверка портов

Для проверки того, что серверы слушают на нужных портах:

#### Windows:

```cmd
netstat -an | findstr "1234 1237 1239 1250"
```

#### Linux:

```bash
ss -tlnp | grep -E '1234|1237|1239|1250'
```

Вы должны увидеть записи со статусом `LISTENING` (Windows) или `LISTEN` (Linux) для каждого порта.

---

## 🔧 Устранение неполадок

### Ошибка: «Порт уже используется» (Address already in use)

**Причина:** Другой процесс занимает требуемый порт.

**Решение:**

```bash
# Linux — найти процесс, занимающий порт (например, 1237):
ss -tlnp | grep 1237

# Windows:
netstat -ano | findstr 1237
```

Завершите мешающий процесс или измените порт в `Config.json`.

---

### Ошибка: «MySQL connection refused» (Отказ в подключении к MySQL)

**Причина:** MySQL не запущен или указаны неверные учётные данные.

**Решение:**
1. Убедитесь, что MySQL работает:
   ```bash
   # Linux:
   sudo systemctl status mysql

   # Windows: проверьте службу MySQL в services.msc
   ```
2. Проверьте правильность параметров в `Config.json`:
   - `Host` — адрес сервера MySQL (обычно `localhost`)
   - `Port` — порт MySQL (обычно `3306`)
   - `User` — имя пользователя (обычно `root`)
   - `Password` — пароль пользователя
   - `Database` — имя базы данных (`aaemu_login` или `aaemu_game`)
3. Убедитесь, что базы данных созданы и схемы импортированы (Шаг 4).

---

### Ошибка: «Config.json not found» (Файл конфигурации не найден)

**Причина:** Файл `Config.json` не был создан.

**Решение:** Скопируйте шаблон и настройте его:

```bash
cp AAEmu.Login/ExampleConfig.json AAEmu.Login/Config.json
cp AAEmu.Game/ExampleConfig.json AAEmu.Game/Config.json
```

Затем отредактируйте оба файла, указав корректные параметры (см. Шаг 5).

---

### Ошибка: «compact.sqlite3 not found» (Отсутствует SQLite-база данных)

**Причина:** Файл `compact.sqlite3` с игровыми данными отсутствует в папке `AAEmu.Game/Data/`.

**Решение:** Убедитесь, что файл `compact.sqlite3` находится по пути `AAEmu.Game/Data/compact.sqlite3`. Этот файл содержит данные об игровых предметах, навыках, NPC и других объектах. Он может распространяться отдельно от исходного кода — обратитесь к [Discord-сообществу](https://discord.gg/vn8E8E6) для получения актуальной версии.

---

### Ошибки сборки (Build errors)

**Причина:** Не установлен .NET SDK или установлена неподходящая версия.

**Решение:**
1. Проверьте версию SDK:
   ```bash
   dotnet --version
   ```
2. Убедитесь, что версия 6.0 или новее.
3. Попробуйте очистить и пересобрать проект:
   ```bash
   dotnet clean
   dotnet restore
   dotnet build
   ```

---

### Игровой сервер не подключается к логин-серверу

**Причина:** Несовпадение `SecretKey` или неверные сетевые настройки.

**Решение:**
1. Убедитесь, что `SecretKey` **одинаковый** в обоих файлах `Config.json`.
2. Убедитесь, что `LoginNetwork` в конфигурации игрового сервера совпадает с `InternalNetwork` в конфигурации логин-сервера:
   - `Host`: `127.0.0.1`
   - `Port`: `1234`
3. Запустите логин-сервер **перед** игровым сервером.

---

## 📁 Важные файлы и директории

| Файл / Директория | Описание |
|---|---|
| `Config.json` | Основной файл конфигурации сервера (в папках `AAEmu.Login/` и `AAEmu.Game/`). |
| `ExampleConfig.json` | Шаблон конфигурации. Используется как основа для создания `Config.json`. |
| `NLog.config` | Настройка системы логирования (уровни логов, файлы, формат). Находится в папках обоих серверов. |
| `AccessLevels.json` | Определяет уровни доступа к GM-командам (`AAEmu.Game/`). Уровень 0 — обычные игроки, 50 — модераторы, 100 — администраторы. |
| `AAEmu.Game/Data/*.json` | Конфигурационные файлы игровых данных (шаблоны персонажей, миры, порталы и др.). |
| `AAEmu.Game/Data/compact.sqlite3` | SQLite-база данных с основными игровыми данными (предметы, навыки, NPC, квесты и т.д.). |
| `SQL/aaemu_login.sql` | SQL-схема базы данных логин-сервера. |
| `SQL/aaemu_game_full.sql` | SQL-схема базы данных игрового сервера. |
| `Logs/` | Директория с лог-файлами сервера (создаётся автоматически при запуске). Содержит `Server.log` и `Error.log`. |
| `publish.sh` | Скрипт для публикации сборок под различные платформы (Windows, Linux). |
| `start_AAEmu.bat` | Windows-скрипт для быстрого запуска обоих серверов. |
| `StartLoginServer.bat` | Windows-скрипт для запуска только логин-сервера. |
| `StartGameServer.bat` | Windows-скрипт для запуска только игрового сервера. |

---

## 🔗 Полезные ссылки

- **Discord-сообщество:** https://discord.gg/vn8E8E6
- **Вики проекта:** https://github.com/l3nsa/archeage3.0/wiki
- **Руководство по участию в разработке:** [CONTRIBUTING.md](../CONTRIBUTING.md)
- **Официальная документация .NET:** https://learn.microsoft.com/ru-ru/dotnet/
- **Скачать .NET SDK:** https://dotnet.microsoft.com/download
- **Скачать MySQL:** https://dev.mysql.com/downloads/installer/

---

## 📝 Краткая сводка команд

```bash
# 1. Клонирование
git clone https://github.com/l3nsa/archeage3.0.git
cd archeage3.0

# 2. Создание конфигурации
cp AAEmu.Login/ExampleConfig.json AAEmu.Login/Config.json
cp AAEmu.Game/ExampleConfig.json AAEmu.Game/Config.json
# Не забудьте отредактировать Config.json!

# 3. Настройка базы данных
mysql -u root -p -e "CREATE DATABASE aaemu_login; CREATE DATABASE aaemu_game;"
mysql -u root -p aaemu_login < SQL/aaemu_login.sql
mysql -u root -p aaemu_game < SQL/aaemu_game_full.sql

# 4. Сборка
dotnet restore
dotnet build

# 5. Запуск (два терминала)
cd AAEmu.Login && dotnet run    # Терминал 1
cd AAEmu.Game && dotnet run     # Терминал 2
```

---

> **Удачного запуска!** Если у вас возникли вопросы, обращайтесь в [Discord-сообщество](https://discord.gg/vn8E8E6). 🎮
