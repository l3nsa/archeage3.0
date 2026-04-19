# Результаты реверс-инженеринга клиента ArcheAge 3.0.3.0

> **Версия клиента**: 3.0.3.0 (2017-03-15, сборка ArcheRage 3.0.3 RU + EU)  
> **Движок**: CryEngine 2 (модифицированный XLGames)  
> **Платформа**: Win32 (x86 PE)  
> **Дата анализа**: 2026-04-19

---

## Оглавление

1. [Структура клиента](#1-структура-клиента)
2. [Карта DLL-зависимостей](#2-карта-dll-зависимостей)
3. [Криптографический стек](#3-криптографический-стек)
4. [Сетевой протокол](#4-сетевой-протокол)
5. [Игровые системы CVar](#5-игровые-системы-cvar)
6. [API xlcommon.dll](#6-api-xlcommondll)
7. [Конфигурация и запуск](#7-конфигурация-и-запуск)
8. [game_pak и ресурсы](#8-game_pak-и-ресурсы)
9. [Маппинг: клиент ↔ сервер](#9-маппинг-клиент--сервер)
10. [Рекомендации по динамическому анализу](#10-рекомендации-по-динамическому-анализу)
11. [План дальнейших действий](#11-план-дальнейших-действий)

---

## 1. Структура клиента

### 1.1 Состав архива (`rage-3.0.3-2019-03-02-bin32.7z`)

```
bin32/                      (83 файла)
├── archeage.exe            2,384,656 байт — точка входа, загрузчик DLL
├── crynetwork.dll          3,747,328 байт — сетевой стек (TCP, шифрование)
├── crygame.dll             2,453,504 байт — игровая логика CryEngine
├── cryaction.dll           8,728,576 байт — фреймворк действий (AI, транспорт, вход в мир)
├── crysystem.dll           1,718,784 байт — системный слой (консоль, MD5, CRC)
├── xlcommon.dll            ???,??? байт   — ядро XLGames (Rijndael, Hash, IO, нити)
├── xlpack.dll              ???,??? байт   — чтение game_pak
├── launcher.bat            — запуск: archeage.exe -r +auth_ip 127.0.0.1:1237 ...
├── account.json            — {"login":"aatest","password":"","token":"31e34f..."}
├── archeageru.ini          — зашифрованный конфиг (не читаемый напрямую)
├── archeagerutest.ini      — зашифрованный тестовый конфиг
├── steam_appid.txt         — "304030" (Steam App ID)
├── debug.log               — WebCoreImpl ошибка, путь E:\Games\ArcheRage NA\bin32\kr
├── d3dcompiler_46.dll      — DirectX shader compiler
├── dbghelp.dll             — Windows debug helper
├── cryphysics.dll          — физический движок
├── cry3dengine.dll         — 3D рендеринг
├── cryrender*.dll          — рендеринг (DX9/DX11)
├── cryinput.dll            — ввод (клавиатура/мышь)
├── crymovie.dll            — видео/катсцены
├── cryfont.dll             — шрифты
├── cryaisystem.dll         — AI система
├── cryanimation*.dll       — анимация
├── crysoundsystem.dll      — звук
└── различные вспомогательные DLL
```

### 1.2 Внешние файлы

| Файл | Размер | Формат |
|------|--------|--------|
| `game_pak` | 34.78 ГБ | Проприетарный XLGames (НЕ ZIP). Заголовок: `DDS ` + маркер `FYRC` на смещении 0x7C |
| `client.version` | ~300 байт | zlib-сжатый (magic: `78 9C`) |
| `xlpack.dll` | PE32 | DLL для чтения game_pak |
| `credits.dat`, `lang.dat` | бинарные | форматы не определены |

---

## 2. Карта DLL-зависимостей

```
archeage.exe
  ├──→ crysystem.dll (CreateSystemInterface)
  │     ├──→ xlcommon.dll (154 функции: Crc32Gen, XLRandom, XlFile*, Data*, X2Log)
  │     ├──→ KERNEL32, USER32, ADVAPI32 (MD5Init/Update/Final через CRT)
  │     └──→ WS2_32 (сетевые сокеты)
  │
  ├──→ crygame.dll (CreateGame, CreateGameStartup, CreateEditorGame)
  │     ├──→ cryaction.dll (CreateGameFramework)
  │     │     ├──→ xlcommon.dll (X2Log, XLRandom, XlTokenizeString)
  │     │     └──→ Scripts/Network/*.xml, scripts/main.lua
  │     └──→ crynetwork.dll (CreateNetwork)
  │           ├──→ xlcommon.dll (CWhirlpoolHash)
  │           ├──→ WS2_32.dll (WSASend, WSARecv, WSAIoctl, CreateIoCompletionPort)
  │           └──→ WININET.dll (HTTP: InternetConnect, HttpSendRequest, HttpOpenRequest)
  │
  └──→ [x2game-dev.dll — ссылка в строках archeage.exe, сама DLL в game_pak]
        Менеджеры: Auctioneer, CofferManager, HeroManager, Mailman, ReputationManager
```

### Ключевые наблюдения

- **crynetwork.dll** — PDB путь: `D:\work\na_hotfix\trunk\dev32\CryNetwork.pdb`
- **Строки сильно обфусцированы** — crynetwork.dll: всего 6 сетевых строк из 50562 общих
- **Singleton**: `"There is already a x2client application running"` (archeage.exe)
- **x2game-dev.dll** — ключевая игровая DLL, НЕ в bin32, предположительно в game_pak

---

## 3. Криптографический стек

### 3.1 Архитектура шифрования (восстановлена из сервера + клиентских DLL)

```
                     ┌─────────────────────────────────────┐
                     │         HANDSHAKE (Level 1)          │
                     │                                     │
                     │  Server → Client: RSA-1024 PubKey   │
                     │    (Modulus 128 bytes + Exp 3 bytes) │
                     │                                     │
                     │  Client → Server: CSAesXorKeyPacket  │
                     │    (opcode 0x163)                   │
                     │    AES-128 key RSA-encrypted        │
                     │    XOR seed RSA-encrypted           │
                     └─────────────┬───────────────────────┘
                                   │
                      ┌────────────┴────────────┐
                      ▼                         ▼
              ┌──────────────┐         ┌──────────────┐
              │   C→S (0x05)  │         │   S→C (0xDD05)│
              │   Level 5     │         │   Level 6     │
              │               │         │               │
              │  XOR → AES    │         │  XOR only     │
              │  (двойной)    │         │  (одинарный)  │
              └──────────────┘         └──────────────┘
```

### 3.2 Магические константы (версия 3.0.3.0)

| Константа | Hex | Применение | В бинарнике? |
|-----------|-----|------------|-------------|
| XOR increment | `0x002FCBD5` | Inline() — генерация XOR-потока | ❌ НЕ найдена как 32-бит literal |
| S→C init | `0x1F2175A0` | `cry = length ^ 0x1F2175A0` | ❌ НЕ найдена |
| XOR key derivation 1 | `0x15A0248E` | `head = (head ^ 0x15A0248E) * head` | ❌ НЕ найдена |
| XOR key derivation 2 | `0x070F1F23` | `^ 0x070F1F23` | ❌ НЕ найдена |
| Secondary sequence | `0x002FA245` | MakeSeq() | ❌ НЕ найдена |
| C→S XOR init | `0x75A024A4` | DecodeXor mixed constant | ❌ НЕ найдена |
| C→S XOR init 2 | `0xC3903B6A` | DecodeXor second XOR | ❌ НЕ найдена |
| CRC8 multiplier | `0x13` | `checksum *= 0x13` | Не проверялась отдельно |
| AES-128 CBC | KeySize=128, BlockSize=128, Padding=None | Через `Rijndael::init()` в xlcommon.dll | ✅ API экспортировано |

**КРИТИЧЕСКИЙ ВЫВОД**: Все крипто-константы НЕ хранятся в crynetwork.dll как 32-битные литералы. Они либо:
- Собираются из нескольких инструкций (например, `MOV + ADD + SHL`)
- Вычисляются в runtime (обфускация)
- Хранятся в зашифрованной секции данных

**Для точного извлечения необходим динамический анализ** (x32dbg на Windows).

### 3.3 Крипто-API через xlcommon.dll

```cpp
// === Rijndael (AES) — 12 экспортированных функций ===
class Rijndael {
    Rijndael();                                              // конструктор
    ~Rijndael();                                             // деструктор
    Rijndael& operator=(const Rijndael&);                    // присваивание
    
    int init(Mode mode, Direction dir, const BYTE* key,      // инициализация
             KeyLength keyLen, BYTE* iv);
    int blockEncrypt(const BYTE* input, int inputLen,        // шифрование блока
                     BYTE* output);
    int blockDecrypt(const BYTE* input, int inputLen,        // дешифрование блока
                     BYTE* output);
    int padEncrypt(const BYTE* input, int inputLen,          // шифрование с паддингом
                   BYTE* output);
    int padDecrypt(const BYTE* input, int inputLen,          // дешифрование с паддингом
                   BYTE* output);
    
    // internal:
    void encrypt(const BYTE in[16], BYTE out[16]);
    void decrypt(const BYTE in[16], BYTE out[16]);
    void keySched(BYTE key[4][?]);
    void keyEncToDec();
};

// === CWhirlpoolHash — 8 экспортированных функций ===
class CWhirlpoolHash {
    static const int DIGESTBYTES;                            // размер дайджеста
    CWhirlpoolHash();
    CWhirlpoolHash(const CryStringT<char>&);                // из строки
    CWhirlpoolHash(const BYTE* data, unsigned int len);     // из буфера
    CWhirlpoolHash& operator=(const CWhirlpoolHash&);
    bool operator==(const CWhirlpoolHash&) const;
    bool operator!=(const CWhirlpoolHash&) const;
    const BYTE* operator()() const;                         // получить дайджест
    CryStringT<char> GetHumanReadable() const;              // hex-строка
    void SerializeWith(CSerializeWrapper<ISerialize>);
    static bool Test();                                     // самотест
};

// === Crc32Gen — 6 экспортированных функций ===
class Crc32Gen {
    Crc32Gen();
    void Init();
    unsigned int GetCRC32(const char* str) const;
    unsigned int GetCRC32(const char* data, int len, unsigned int seed) const;
    unsigned int GetCRC32Lowercase(const char* str) const;
    unsigned int GetCRC32Lowercase(const char* data, int len, unsigned int seed) const;
};

// === Автономные хеш-функции ===
unsigned int   MurmurHash2(const void* key, int len, unsigned int seed);
unsigned __int64 MurmurHash64A(const void* key, int len, unsigned __int64 seed);
unsigned __int64 CityHash64(const char* buf, unsigned int len);
pair<uint64,uint64> CityHash128(const char* buf, unsigned int len);
unsigned int   SuperFastHash(const char* data, int len);
void           sha1(BYTE* data, int len, BYTE digest[20]);
void           sha256_password(const char* pass, const char* salt, BYTE* out);
void           MD5Init/MD5Update/MD5Final/MD5Transform (через crysystem.dll)
void           md5(...)                                 (через xlcommon.dll)
```

### 3.4 Верификация крипто-реализации сервера

| Аспект | Сервер (EncryptionManager.cs) | Клиент (xlcommon.dll) | Соответствие |
|--------|------------------------------|----------------------|-------------|
| RSA handshake | RSACryptoServiceProvider, 1024-bit | Через crynetwork.dll (не в экспортах) | ✅ |
| AES decryption | RijndaelManaged, CBC, 128-bit, No padding | Rijndael class: init(Mode, Dir, Key, KeyLen, IV) | ✅ API совместимо |
| XOR stream | Inline() с 0x2FCBD5 | Не видно в экспортах (internal) | ⚠️ Нужна верификация |
| CRC8 | `checksum *= 0x13; checksum += data[i]` | Не экспортируется | ⚠️ Нужна верификация |
| Whirlpool hash | Используется для проверок | CWhirlpoolHash class | ✅ |
| Key derivation | `head ^ 0x15A0248E * head ^ 0x070F1F23` | Внутри crynetwork.dll, обфусцировано | ⚠️ Критично |

---

## 4. Сетевой протокол

### 4.1 Уровни пакетов

| Level | Direction | Название | Шифрование | Описание |
|-------|-----------|----------|-----------|----------|
| 1 | C↔S | Plain | Нет | Начальные пакеты (handshake, key exchange) |
| 2 | C→S | Proxy | Нет | Для Stream-сервера |
| 3 | C→S | Deflate | zlib | Сжатые (редко) |
| 4 | C→S | Deflate | zlib | Сжатые (редко) |
| 5 | C→S | Encrypted | AES-CBC-128 + XOR | Все игровые пакеты от клиента |
| 6 | S→C | Encrypted | XOR only | Все игровые пакеты от сервера |

### 4.2 Формат пакета

```
C→S Level 5 (зашифрованный):
┌──────┬─────┬───────┬──────────────────────────┐
│ len  │ 00  │ 05    │ encrypted_payload         │
│ u16  │ u8  │ u8    │ [XOR → AES encrypted]     │
└──────┴─────┴───────┴──────────────────────────┘

C→S Level 1 (plain):
┌──────┬─────┬───────┬─────┬─────────┬──────────┐
│ len  │ 00  │ 01    │ crc │ counter │ payload  │
│ u16  │ u8  │ u8    │ u8  │ u8      │ [opcode u16 + data] │
└──────┴─────┴───────┴─────┴─────────┴──────────┘

S→C Level 6:
┌──────┬─────┬───────┬──────────────────────────┐
│ len  │ 00  │ DD05  │ XOR_encrypted_payload     │
│ u16  │ u8  │ u16   │ [XOR encrypted]           │
└──────┴─────┴───────┴──────────────────────────┘
```

### 4.3 Поток подключения (восстановлен)

```
1. Клиент запускается: archeage.exe -r +auth_ip 127.0.0.1:1237 -uid aatest -token TOKEN

2. АУТЕНТИФИКАЦИЯ (Login Server, порт 1237):
   C→L: CARequestAuthPacket (или MailRu/Trion/Tencent/TW/GameOn вариант)
   L→C: ACChallengePacket (RSA public key)
   C→L: CAChallengeResponsePacket (зашифрованные credentials)
   L→C: ACAuthResponsePacket (успех/отказ)
   C→L: CAListWorldPacket
   L→C: ACWorldListPacket (список серверов)
   C→L: CAEnterWorldPacket
   L→C: ACWorldCookiePacket (cookie для Game Server)

3. ПОДКЛЮЧЕНИЕ К GAME SERVER:
   Клиент подключается по cl_serveraddr:cl_serverport
   
   S→C: X2EnterWorldResponsePacket (0x000, Level 1)
        → содержит RSA Public Key (Modulus + Exponent)
   
   C→S: CSAesXorKeyPacket (0x163, Level 1)
        → AES-128 ключ зашифрованный RSA
        → XOR seed зашифрованный RSA
   
   ПЕРЕКЛЮЧЕНИЕ НА ШИФРОВАННЫЙ РЕЖИМ
   
   C→S: CSMoveUnitPacket, CSChatMessagePacket... (Level 5)
   S→C: SCUnitMovementsPacket, SCChatMessagePacket... (Level 6/DD05)
```

### 4.4 Таблицы опкодов

**Client → Server (CS): 419 опкодов** (CSOffsets.cs, версия 3.0.3.0)

| Категория | Примеры опкодов | Количество |
|-----------|----------------|-----------|
| Подключение/мир | X2EnterWorld=0x000, LeaveWorld, EnterWorld | ~10 |
| Движение | MoveUnit=0x084, Rotation, Jump | ~15 |
| Криптография | AesXorKey=0x163 | 1 |
| Чат | ChatMessage, SendMail | ~10 |
| Предметы | BuyItems, SellItems, MoveItem, DestroyItem | ~40 |
| Скиллы | UseSkill, LearnSkill, StartCraft | ~20 |
| Квесты | StartQuest, CompleteQuest, AbandonQuest | ~15 |
| Жильё | PlaceHouse, DestroyHouse, PayTax | ~20 |
| Транспорт | SpawnSlave, DespawnSlave, BoardVehicle | ~15 |
| Гильдии | CreateExpedition, DisbandExpedition, InviteMember | ~15 |
| Торговля | StartTrade, AcceptTrade, AddTradeItem | ~10 |
| PvP | DuelChallenge, FactionDeclareHostile | ~10 |
| Аукцион | RegisterAuctionItem, BidAuctionItem | ~10 |
| Прочие | ~230 |

**Server → Client (SC): 762 опкода** (SCOffsets.cs, версия 3.0.3.0)

| Категория | Примеры | Количество |
|-----------|---------|-----------|
| Начальные | X2EnterWorldResponse=0x000, InitialConfig=0x034 | ~20 |
| Unity/объекты | UnitMovements, SpawnUnit, DespawnUnit | ~30 |
| Инвентарь | ItemUpdate, InventoryFull, BagOpen | ~40 |
| Статусы | HealthUpdate, ManaUpdate, BuffAdd, BuffRemove | ~30 |
| UI | ShowUI, HideUI, ShowDialog | ~20 |
| Прочие | ~620 |

---

## 5. Игровые системы CVar

### 5.1 Сетевые CVar (извлечены из строк cryaction.dll)

| CVar | Значение по умолчанию | Назначение |
|------|----------------------|-----------|
| `cl_serveraddr` | — | IP адрес game-сервера |
| `cl_serverport` | — | Порт game-сервера |
| `sv_port` | — | Порт для серверного режима |
| `cl_account_id` | — | ID аккаунта |
| `cl_password` | — | Пароль (shadow) |
| `cl_zone_id` | — | ID зоны для входа |
| `cl_world_cookie` | — | Cookie от Login Server |
| `cl_web_session_key` | — | Сессионный ключ |
| `cl_packetRate` | — | Частота отправки пакетов (клиент) |
| `sv_packetRate` | — | Частота отправки пакетов (сервер) |
| `sv_timeout_disconnect` | — | Таймаут до отключения |
| `g_localPacketRate` | — | Локальная частота пакетов |

### 5.2 Аутентификационные CVar

| CVar | Источник |
|------|---------|
| `auth_ip` | Из командной строки (`+auth_ip 127.0.0.1:1237`) |
| `uid` | Из командной строки (`-uid aatest`) |
| `token` | Из командной строки (`-token 31E34F...`) |

### 5.3 Игровые системы (из строк cryaction.dll, 573 строки)

Обнаружены строки для всех основных систем CryEngine:
- **AI**: AIMovementAbility, AICharacter, AIOBJECT_VEHICLE, AISS_SPAWNING/DESPAWNING
- **Транспорт**: CVehicleMovement*, ACT_ENTERVEHICLE/EXITVEHICLE
- **Анимация**: CAnimatedCharacter, ExactPositioning
- **Зоны**: `LOADING: Loading Zone %s`, `seamlessZone`
- **Спавн**: SpawnGroup, SpawnLocation, RequestSpawnGroup
- **Чат**: AddChatText, ChatMonitor, SendChatMessage
- **Друзья**: AddFriendAccepted, AddFriendDeclined
- **Предметы**: CarryItem, DropItem, PickItem, inventory_full
- **Инвентарь**: AddInventoryAmmo, AccessoryAmmoAmount
- **Мультиплеер**: ConnectToServer, Disconnect, eGE_Connected, eGE_Disconnected

### 5.4 Скрипты и конфиги (пути из строк)

```
/Scripts/Network/EntityScheduler.xml
/Scripts/Network/cvars.txt
/Scripts/Network/server_cvars.txt
scripts/main.lua
Game\%s\TweaksSave.lua
Libs/UI/GlobalEnums.xml
Libs/UI/MP_Objectives.xml
Libs/UI/StatusMessages.xml
Libs/UI/WeaponAccessories.xml
libs/config/defaultProfile.xml
libs/config/profiles/default/attributes.xml
fonts/hud.xml
config/diff_easy.cfg
config/diff_normal.cfg
config/diff_hard.cfg
%USER%/config/server.cfg
```

---

## 6. API xlcommon.dll

### 6.1 Полная статистика экспортов

| Категория | Количество | Ключевые функции |
|-----------|-----------|------------------|
| Crypto (Rijndael/AES) | 12 | init, blockEncrypt, blockDecrypt, padEncrypt, padDecrypt |
| Crypto (Hash/CRC) | 70 | CWhirlpoolHash, Crc32Gen, CityHash64/128, MurmurHash*, SHA1, SHA256, MD5 |
| Crypto (Random) | 11 | XLRandom, CryRandom, CryRandomSeed |
| Crypto (Base64) | 12 | XlBase64Encode/Decode, XlHexStrToBin, XlDecodeUrl |
| File/IO | 117 | XlOpenFile, XlReadFile, XlCreateDirectory, XlFileExists и т.д. |
| String/Memory | 96 | XlCopyString, CryMalloc/CryFree, MemoryOutputStream |
| Threading | 11 | XlCreateMutex, XlCreateEvent, XlCreateSemaphore |
| Data Structures | 96 | Data class (XML-like), ExtendedStack |
| Other | 242 | CMTRand_int32, TimeKeeper, XlPing, X2Log*, Debug |
| **ИТОГО** | **~567** | |

### 6.2 Data class API (XML/конфиг парсинг)

```cpp
class Data {
    Data(const char* name);
    ~Data();
    
    // Навигация
    Data* Attribute(const char* name) const;
    Data* Find(const char* path) const;
    Data* ChildAt(int index) const;
    Data* ChildWithValue(const char* value) const;
    int Size() const;
    int Index() const;
    
    // Чтение значений
    const char* StrValue() const;
    const char* StrAttribute(const char* name, const char* default) const;
    int IntAttribute(const char* name, int default) const;
    float FloatAttribute(const char* name, float default) const;
    double DoubleAttribute(const char* name, double default) const;
    bool BoolAttribute(const char* name, bool default) const;
    __int64 Int64Attribute(const char* name, __int64 default) const;
    
    // Запись
    void SetValue(const char*);
    void SetValue(int); void SetValue(float); void SetValue(double); void SetValue(bool);
    void SetAttribute(const char* name, const char* value);
    void SetAttribute(const char* name, int value);
    void Add(Data* child);
    void Remove();
    void Clear();
    Data* Clone() const;
    
    // Сериализация
    bool Load(const char* filename, int flags);
    bool LoadFromFile(const char* filename, bool* success);
    bool Save(const char* filename, bool pretty) const;
    bool SaveToBuffer(char* buf, int bufSize) const;
    void Path(char* out, int maxLen);
};
```

---

## 7. Конфигурация и запуск

### 7.1 Командная строка

```bash
archeage.exe -r +auth_ip 127.0.0.1:1237 -uid aatest -token 31E34F2B72D93BB25D5F27BE8A94C478
```

| Параметр | Описание |
|----------|---------|
| `-r` | Режим запуска (без launcher) |
| `+auth_ip` | IP:порт Login сервера |
| `-uid` | Имя пользователя |
| `-token` | Токен аутентификации (32 hex символа = 16 байт) |

### 7.2 account.json

```json
{
  "login": "aatest",
  "password": "",
  "token": "31e34f2b72d93bb25d5f27be8a94c478"
}
```

### 7.3 Зашифрованные конфиги

`archeageru.ini` и `archeagerutest.ini` зашифрованы — внутри данных видны маркеры `ArcheageRUTest.ini` и `ArcheageRU`, но содержимое не читается. Шифрование предположительно через Rijndael из xlcommon.dll с фиксированным ключом.

### 7.4 Steam интеграция

- App ID: **304030** (ArcheAge)
- Наличие `steam_api.dll` в bin32 (предположительно)

---

## 8. game_pak и ресурсы

### 8.1 Формат game_pak

- **Размер**: 34,781,554,688 байт (32.39 ГБ)
- **НЕ ZIP формат** — нет ZIP EOCD/ZIP64 сигнатур
- **Заголовок**: начинается с `DDS ` (DXT1 текстура), маркер `FYRC` на 0x7C
- **Хвост**: заполнен нулями
- **Читается**: через xlpack.dll (проприетарный формат XLGames)

### 8.2 Предположительное содержимое (из строк DLL)

На основании путей в строках бинарников, game_pak должен содержать:

```
Scripts/
  Network/
    EntityScheduler.xml    — расписание сущностей
    cvars.txt              — клиентские переменные
    server_cvars.txt       — серверные переменные
  main.lua                 — точка входа Lua

Libs/
  UI/
    HUD_Crosshair.gfx      — Flash UI
    GlobalEnums.xml         — перечисления
    MP_Objectives.xml       — цели мультиплеера
    StatusMessages.xml      — статусные сообщения
    WeaponAccessories.xml   — аксессуары оружия

libs/config/
  defaultProfile.xml
  profiles/default/attributes.xml

config/
  diff_easy.cfg
  diff_normal.cfg
  diff_hard.cfg  

fonts/
  hud.xml

Game/<имя>/
  TweaksSave.lua           — сохранение настроек

x2game-dev.dll             — основная игровая DLL (менеджеры: Auctioneer, Coffer, Hero, Mail, Reputation)
```

### 8.3 Извлечение game_pak

Для извлечения необходимо:
1. Написать парсер формата на основании RE xlpack.dll
2. ИЛИ использовать xlpack.dll напрямую на Windows через LoadLibrary + GetProcAddress
3. ИЛИ использовать существующие community-инструменты для game_pak

---

## 9. Маппинг: клиент ↔ сервер

### 9.1 Менеджеры клиента vs. менеджеры сервера

| Клиент (из строк) | Сервер (AAEmu.Game) | Статус |
|-------------------|---------------------|--------|
| Auctioneer | AuctionManager.cs | ✅ Реализован |
| CofferManager | — | ❌ Отсутствует |
| HeroManager | — | ❌ Отсутствует |
| Mailman | MailManager.cs | ✅ Реализован |
| ReputationManager | FactionManager.cs (частично) | ⚠️ Частично |
| ChatMonitor | ChatManager.cs | ✅ Реализован |
| ConnectToServer | GameConnection.cs | ✅ Реализован |
| SpawnGroup/Location | SpawnManager.cs | ✅ Реализован |
| VehicleMovement* | BoatPhysicsManager.cs | ⚠️ Частично |

### 9.2 Реализация пакетов

| Метрика | Значение | Процент |
|---------|---------|---------|
| CS опкодов (таблица) | 419 | 100% |
| CS пакетных файлов | 259 | 61.8% |
| CS с Read() реализацией | 256 | 61.1% |
| CS stub-файлов (<20 строк) | ~30 | 7.2% |
| SC опкодов (таблица) | 762 | 100% |
| SC пакетных файлов | 343 | 45.0% |

### 9.3 Реализация серверных менеджеров

**Полностью реализованы (66 менеджеров):**
- AccountManager, AuctionManager, CashShopManager, ChatManager
- CraftManager, DuelManager, ExpeditionManager, FamilyManager
- HousingManager, ItemManager, MailManager, MateManager
- QuestManager, SkillManager, SlaveManager, TeamManager
- TradeManager, TransferManager, WorldManager, ZoneManager
- CharacterManager, DoodadManager, NpcManager, SpawnManager
- 14 IdManager'ов и другие

**Отсутствуют (нужна реализация):**
- CofferManager (сундуки/хранилища)
- HeroManager (система героев)
- ReputationManager (полная система репутации)
- PvP/WarManager (полные PvP механики)
- FishingManager (рыбалка)
- MusicManager (музыкальная система)
- x2game-dev.dll менеджеры

---

## 10. Рекомендации по динамическому анализу

### 10.1 Что НЕЛЬЗЯ извлечь статически

1. **Крипто-константы** — обфусцированы, нужен x32dbg
2. **Точный формат пакетов** — для 419-160=259 нереализованных CS и 762-343=419 нереализованных SC
3. **game_pak содержимое** — проприетарный формат
4. **Взаимодействие x2game-dev.dll** — основная игровая логика
5. **Зашифрованные INI** — ключ шифрования неизвестен

### 10.2 Настройка для динамического анализа

```
Требования:
- Windows 7/10 x86 или x64
- x32dbg (обязательно 32-бит, т.к. клиент PE32)
- IDA Pro 7+ или Ghidra 10+
- Wireshark (для перехвата пакетов)

Шаг 1. Установить breakpoint на Rijndael::init в xlcommon.dll
  → Получить AES ключ, IV и Mode при каждом вызове
  
Шаг 2. Установить breakpoint на CWhirlpoolHash конструкторы
  → Отследить входные данные для хеширования

Шаг 3. Установить breakpoint на WSASend/WSARecv в crynetwork.dll
  → Перехватить сырые пакеты до/после шифрования

Шаг 4. Трассировка вызова StoreClientKeys:
  → Найти, где 0x15A0248E и 0x070F1F23 формируются
  → Отследить XOR seed → XOR key деривацию

Шаг 5. Breakpoint на Rijndael::blockDecrypt
  → key и IV уже будут в структуре Rijndael
  → Сравнить с серверной реализацией DecodeAes
```

### 10.3 Ключевые адреса для IDA/Ghidra

```
crynetwork.dll:
  Export: CreateNetwork            — точка входа сетевого модуля
  Import: WSASend (WS2_32.dll)    — отправка данных
  Import: WSARecv (WS2_32.dll)    — получение данных
  Import: CWhirlpoolHash (xlcommon.dll) — хеширование
  PDB: D:\work\na_hotfix\trunk\dev32\CryNetwork.pdb

crygame.dll:
  Export: CreateGame               — инициализация
  Export: CreateGameStartup        — запуск
  Strings: ConnectToServer*, connect %d.%d.%d.%d:%d

cryaction.dll:
  Export: CreateGameFramework      — основной фреймворк
  Strings: ConnectToServer, cl_serveraddr, cl_serverport, cl_account_id
  Scripts: /Scripts/Network/EntityScheduler.xml

archeage.exe:
  Strings: x2game-dev.dll, Auctioneer, CofferManager, HeroManager
  Singleton: "There is already a x2client application running"
```

---

## 11. План дальнейших действий

### Фаза A: Извлечение game_pak (приоритет: ВЫСОКИЙ)

| # | Задача | Оценка сложности |
|---|--------|-----------------|
| A1 | RE xlpack.dll — восстановить формат game_pak | Средняя |
| A2 | Извлечь x2game-dev.dll из game_pak | Средняя |
| A3 | Извлечь Scripts/Network/*.xml, cvars.txt | Низкая (после A1) |
| A4 | Извлечь Libs/UI/*.xml конфиги | Низкая (после A1) |
| A5 | Извлечь GameData SQLite/XML (предметы, NPC, квесты) | Средняя (после A1) |

### Фаза B: Динамический анализ crynetwork.dll (приоритет: ВЫСОКИЙ)

| # | Задача | Оценка сложности |
|---|--------|-----------------|
| B1 | Setup x32dbg + запуск клиента с локальным Login Server | Низкая |
| B2 | BP на Rijndael::init — извлечь параметры AES | Низкая |
| B3 | BP на WSASend/WSARecv — дамп пакетов | Низкая |
| B4 | Трассировка XOR key derivation (0x15A0248E, 0x070F1F23) | Средняя |
| B5 | Верификация серверных констант vs. клиентские | Средняя |
| B6 | Дамп полной таблицы опкодов из dispatch-функции | Высокая |

### Фаза C: RE x2game-dev.dll (приоритет: СРЕДНИЙ)

| # | Задача | Оценка сложности |
|---|--------|-----------------|
| C1 | Извлечь и проанализировать экспорты x2game-dev.dll | Средняя |
| C2 | Восстановить API: Auctioneer, CofferManager, HeroManager | Высокая |
| C3 | Маппинг опкодов на конкретные функции | Высокая |
| C4 | Реализовать CofferManager в серверном эмуляторе | Средняя |
| C5 | Реализовать HeroManager в серверном эмуляторе | Средняя |

### Фаза D: Заполнение пробелов сервера (приоритет: СРЕДНИЙ)

| # | Задача | Описание |
|---|--------|---------|
| D1 | Реализовать 30 stub C→S пакетов | Пакеты <20 строк |
| D2 | Реализовать 160 отсутствующих C→S обработчиков | Нет файлов для опкодов |
| D3 | Реализовать 419 отсутствующих S→C пакетов | Нет файлов для опкодов |
| D4 | Верификация формата каждого пакета через Wireshark | Запись и анализ трафика |

### Фаза E: Полный протокольный тест (приоритет: НИЗКИЙ)

| # | Задача |
|---|--------|
| E1 | Запуск Login + Game серверов |
| E2 | Подключение клиента, запись полного handshake |
| E3 | Вход в мир, walk/run/jump — верификация movement |
| E4 | Чат, торговля, крафт — верификация payload |
| E5 | Stress-тест: 100+ подключений |

---

## Приложение: Быстрая справка для разработчика

### Крипто-формулы сервера (из EncryptionManager.cs)

```csharp
// === XOR key derivation (StoreClientKeys) ===
uint head = RSA_Decrypt(xorKeyEncrypted);
head = (head ^ 0x15A0248E) * head ^ 0x070F1F23;  // version-specific!
uint xorKey = head * head;

// === S→C XOR stream (StoCEncrypt) ===
uint cry = (uint)(length ^ 0x1F2175A0);
for (i = 4*(len/4)-1; i >= 0; i--)
    output[i] = input[i] ^ Inline(ref cry);
// where Inline: cry += 0x2FCBD5; n = (cry >> 16) & 0xF7; return n==0 ? 0xFE : n;

// === C→S XOR+AES (Decode) ===
// Step 1: DecodeXor с XOR key, variable offset (seq % 3/5/7/9/11)
// Step 2: DecodeAes с AES-128-CBC, IV = последние 16 байт предыдущего пакета

// === CRC8 ===
uint checksum = 0;
for (i=0; i<len; i++) { checksum *= 0x13; checksum += data[i]; }
return (byte)checksum;
```

### Опкоды первостепенной важности

```
CS 0x000  X2EnterWorldPacket     — вход в мир
CS 0x084  CSMoveUnitPacket       — движение
CS 0x163  CSAesXorKeyPacket      — обмен ключами
SC 0x000  X2EnterWorldResponse   — ответ входа + RSA ключ
SC 0x034  SCInitialConfigPacket  — начальная конфигурация
```
