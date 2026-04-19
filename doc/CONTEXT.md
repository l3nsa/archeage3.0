# Контекстный файл проекта: ArcheAge 3.0 Server Emulator

> **Ветка**: `copilot/aaemu-3030-compat-fixes`  
> **Целевой клиент**: ArcheAge 3.0.3.0 (2017-03-15)  
> **Платформа**: .NET 6.0 / C#  
> **Дата анализа**: 2026-04-19

---

## 1. Архитектура проекта

### Проекты (солюшн `AAEmu.sln`)

| Проект | Назначение |
|--------|-----------|
| `AAEmu.Commons` | Общие утилиты: сеть, криптография, сжатие, БД |
| `AAEmu.Login` | Login-сервер (аутентификация, список серверов) |
| `AAEmu.Game` | Game-сервер (все игровые системы) |

### Ключевые модули

```
AAEmu.Commons/
  Cryptography/
    EncryptionManager.cs   — XOR/AES/RSA шифрование пакетов
    ConnectionKeychain.cs  — хранение ключей на соединение
  Network/
    Session.cs             — базовый TCP-сокет
    PacketStream.cs        — сериализация/десериализация
    PacketMarshaler.cs     — публичный маршалер
    BaseProtocolHandler.cs — базовый обработчик протокола

AAEmu.Game/
  Core/Network/Game/
    GameNetwork.cs         — регистрация ВСЕХ C→S опкодов (~420 CS)
    GameProtocolHandler.cs — разбор входящих пакетов по уровням 1/5
  Core/Packets/C2G/
    CSOffsets.cs           — таблица опкодов C→S (419 констант)
    *.cs                   — ~259 файлов реализации CS-пакетов
  Core/Packets/G2C/
    SCOffsets.cs           — таблица опкодов S→C (762 константы)
    *.cs                   — ~343 файла реализации SC-пакетов
  Core/Managers/           — 68 менеджеров игровых подсистем
```

---

## 2. Сетевой протокол

### Структура пакета (C→S и S→C)

```
[2 байта] length       — длина тела
[1 байт]  unknown      — зарезервировано
[1 байт]  level        — тип пакета:
                          1 = обычный (plaintext)
                          2 = proxy
                          3/4 = deflate (сжатый)
                          5 = encrypted (C→S, AES+XOR)
                          6 = encrypted (S→C, XOR)
[2 байта] opcode/type  — ID пакета
[N байт]  body         — полезная нагрузка
```

### Уровни шифрования (level field)

- **Level 1** — открытые текст, используется на старте сессии
- **Level 5** — клиент→сервер: AES-CBC (128-bit) + XOR-поток
- **Level 6** (DD05) — сервер→клиент: XOR-поток на основе длины пакета

---

## 3. Криптографическая схема (полностью реализована)

### Рукопожатие (handshake)

```
1. Сервер → Клиент: RSA public key (1024-bit)
   Файл: EncryptionManager.WriteKeyParams()
   Пакет: SC, отправляет modulus (128 байт) + exponent (3 байта)

2. Клиент → Сервер: CSAesXorKeyPacket (level=1, opcode=0x163)
   Тело: [RSA_enc(AES_key_16b)] + [RSA_enc(XOR_seed_4b)]
   Файл: CSAesXorKeyPacket.cs → EncryptionManager.StoreClientKeys()

3. Деривация ключей (файл EncryptionManager.StoreClientKeys):
   raw_xor = RSA_decrypt(xor_encrypted)
   head    = BitConverter.ToUInt32(raw_xor, 0)
   head    = (head ^ 0x15A0248E) * head ^ 0x070F1F23  // Magic для 3.0.3.0
   XorKey  = head * head & 0xFFFFFFFF
   AesKey  = RSA_decrypt(aes_encrypted)  // 16 байт

4. Для level=5 пакетов этой же сессии — существует дубль-пакет
   CSAesXorKey_05_Packet (opcode=0x163, level=5): тот же процесс
```

### S→C шифрование (StoCEncrypt / ByteXor)

```csharp
cry = (uint)(length ^ 0x1F2175A0)
for i in range(length):
    keyByte = Inline(ref cry)           // cry += 0x2FCBD5; n = (cry>>16)&0xF7; return n==0 ? 0xFE : n
    out[i] = in[i] ^ keyByte
```

### C→S расшифровка (Decode)

```
1. XOR-декодирование (DecodeXor) с ключом XorKey
2. AES-CBC декодирование (DecodeAes) с ключом AesKey и IV
```

### CRC8 (контрольная сумма)

```csharp
checksum = 0
for each byte b:
    checksum = checksum * 0x13 + b
result = (byte)checksum
```

---

## 4. Таблица опкодов (версия 3.0.3.0)

### C→S (Client → Server) — файл CSOffsets.cs

| Опкод | Имя пакета | Уровень | Описание |
|-------|------------|---------|----------|
| 0x000 | X2EnterWorldPacket | 1 | Вход в мир |
| 0x008 | CSStartDuelPacket | 5 | Начало дуэли |
| 0x084 | CSMoveUnitPacket | 5 | Движение юнита |
| 0x163 | CSAesXorKeyPacket | 1/5 | Передача ключей шифрования |
| 0x186 | CSResturnAddrsPacket | 1/5 | Возврат адресов |
| 0x15d | CSHgResponsePacket | 1/5 | HG-ответ (heartbeat guard) |
| 0x0eb | CSLeaveWorldPacket | 5 | Выход из мира |
| 0x084 | CSMoveUnitPacket | 5 | Передвижение |
| ... | **(419 всего)** | | |

### S→C (Server → Client) — файл SCOffsets.cs

| Опкод | Имя пакета | Описание |
|-------|------------|---------|
| 0x000 | X2EnterWorldResponsePacket | Ответ на вход в мир |
| 0x034 | SCInitialConfigPacket | Начальная конфигурация |
| 0x1e5 | SCReconnectAuthPacket | Аутентификация реконнекта |
| 0x21a | SCPrepareLeaveWorldPacket | Подготовка к выходу |
| ... | **(762 всего)** | |

---

## 5. Текущее состояние реализации

### Что реализовано

- ✅ TCP-сервер: Login (порт 1239) и Game (порт 1237)
- ✅ RSA-рукопожатие и обмен ключами
- ✅ XOR-шифрование S→C (level 6 / DD05)
- ✅ AES+XOR-дешифрование C→S (level 5 / 0005)
- ✅ CRC8-контрольная сумма пакетов
- ✅ Система регистрации пакетов по опкоду+уровню
- ✅ Создание/удаление персонажей
- ✅ Вход в мир, базовое движение
- ✅ Спавн NPC/Doodad
- ✅ Движение NPC по путям
- ✅ Базовая система скиллов
- ✅ Базовая система крафта
- ✅ Базовая система квестов (QuestManager, 1540 строк)
- ✅ Базовая система жилья
- ✅ Аукцион
- ✅ Торговля между игроками
- ✅ Инвентарь, экипировка
- ✅ Гильдии/Экспедиции

### Что НЕ реализовано / сломано

- ❌ Валидация пароля при логине (`// TODO validation password`)
- ❌ Challenge-response аутентификация (закомментирована)
- ❌ Динамический WSK-ключ (захардкожен `"65CCBF5AF8DB8B633D3C03C5A8735601"`)
- ❌ 25 методов выбрасывают NotImplementedException (Route-классы)
- ❌ Формулы расчёта (min/max/clamp/floor/log/sqrt отсутствуют)
- ❌ Полная боевая система (Combat.cs — заглушки)
- ❌ 240+ файлов содержат TODO-комментарии

---

## 6. База данных

- **Движок**: MySQL/MariaDB
- **Конфиг**: `AAEmu.Game/ExampleConfig.json`
- **SQL-схемы**:
  - `AAEmu.Game/SQL/aaemu_game_2019.05.07.sql`
  - `SQL/` (корневая директория)

---

## 7. Зависимости

- .NET 6.0 (обновлено с 2.2)
- NLog (логирование)
- MySql.Data (БД)
- System.Security.Cryptography (RSA/AES)

---

## 8. Ключевые файлы для исследования

| Файл | Зачем |
|------|-------|
| `AAEmu.Commons/Cryptography/EncryptionManager.cs` | Полная реализация крипто |
| `AAEmu.Commons/Cryptography/ConnectionKeychain.cs` | Структура ключей соединения |
| `AAEmu.Game/Core/Network/Game/GameNetwork.cs` | Регистрация всех CS-пакетов |
| `AAEmu.Game/Core/Packets/C2G/CSOffsets.cs` | Таблица CS-опкодов |
| `AAEmu.Game/Core/Packets/G2C/SCOffsets.cs` | Таблица SC-опкодов |
| `AAEmu.Game/Core/Network/Game/GameProtocolHandler.cs` | Разбор пакетов |
| `AAEmu.Login/Core/Controllers/LoginController.cs` | Логика аутентификации |
