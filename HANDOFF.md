# HANDOFF — Unity Sahne & Asset Kurulumu

Bu döküman, **kod tarafı bittikten sonra** Unity Editor'da yapılacak işleri sıfırdan adım adım anlatır.
Hedef: bir başkası repo'yu klonlayıp bu dokümanı takip ederek oyunu sahne+prefab+SO seviyesinde oynanabilir hale getirebilmeli.

Oyun: **6 kişilik Co-op PvE Tower Defense.** Kale içi merkez, kuzey deniz (gemi wave'leri), doğu/batı ticaret yolu (kervan + haydut). Photon PUN 2 + Photon Voice ile network.

---

## 0) Önkoşullar

- **Unity sürümü**: Projeyi açacağın sürüm `ProjectSettings/ProjectVersion.txt` içinde yazıyor (LTS önerilir, 2022.3.x veya üzeri).
- **Photon hesabı**: https://www.photonengine.com adresinden ücretsiz hesap aç.
  - "PUN" tipi bir uygulama oluştur → **App ID**'yi kopyala.
  - "Voice" tipi ayrı bir uygulama oluştur → **Voice App ID**'yi kopyala.
- **Git LFS**: Sahne/prefab/model girince repoya büyük binary'ler gelecek. `git lfs install` çalıştır.

---

## 1) Repo'yu açma ve ilk derleme

```
git clone git@github.com:Porphyri0n/SinirsizcilarGameProject.git
cd SinirsizcilarGameProject
```

Unity Hub'dan `Open → klasörü seç`. **İlk açılış uzun sürer** (Library/ klasörü yeniden üretiliyor).

Açılınca:
- Console'da **kırmızı hata olmamalı**. Sarı uyarı (Photon'dan AppID isteyen) normal.
- Sol altta dönen ikon biterse import tamamdır.

### Photon AppID girme

`Tools → Photon Unity Networking → Highlight Server Settings` (ya da `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`)
- **App Id PUN** alanına PUN AppID'ni yapıştır.
- **App Id Voice** alanına Voice AppID'ni yapıştır.
- Fixed Region: `eu` (kod default'u bu, kodda `NetworkManager.fixedRegion` ile aynı bırak).

---

## 2) Tag ve Layer ayarı (KRİTİK)

`Edit → Project Settings → Tags and Layers`

### Tags (eklenecekler)
Kod bu tag'leri arıyor (`GameConstants.cs:60-69`):

- `Player`
- `Enemy`
- `Defense`
- `Castle`
- `Interactable`
- `Loot`
- `Caravan`
- `Bandit`
- `TradeRoad`

### Layers (eklenecekler)
- `Ground`
- `Water`
- `WallTop` (sur üstü yürünebilir alan — oyuncu burada hareket edebilir)

> Tag/Layer isimleri **birebir** olmak zorunda. Yanlış yazarsan kod tag karşılaştırması başarısız olur ve sistemler sessizce kırılır.

---

## 3) Klasör yapısı (zaten var, dokunma)

```
Assets/
├── Scripts/        ← Kod (hazır, ellemiyorsun)
├── Prefabs/        ← Buraya prefab'ları koyacaksın (alt klasörler boş ama hazır)
│   ├── Bandits/ Caravans/ Defenses/ Resources/ Ships/ UI/ Weapons/
├── Data/           ← ScriptableObject (.asset) dosyaları burada
│   ├── Bandits/ Caravans/ Defenses/ Potions/ Recipes/ Resources/
│   ├── Ships/ Upgrades/ Waves/ Weapons/ Wheelbarrow/
└── Photon/         ← Photon paketi (ellemiyorsun)
```

Senin yapacağın yeni klasör: **`Assets/Scenes/`** ve içine `Lobby.unity` + `Game.unity`.

---

## 4) ScriptableObject (SO) asset'lerini üret

SO'lar Inspector'da doldurulan veri dosyaları. Kod hepsini "data driven" okuyor; SO yoksa hiçbir şey çalışmaz.

Her birini şu şekilde oluştur:
- Project pencerede ilgili `Assets/Data/...` klasörüne sağ tık
- `Create → Game → <Data tipi>` (her SO scripti `[CreateAssetMenu]` ile bu menüye kayıtlı)
- Inspector'da alanları doldur

### 4.1 ResourceData (`Assets/Data/Resources/`)
6 adet (her `ResourceType` için bir tane): `Wood`, `Stone`, `Iron`, `Steel`, `Gold`, `Crystal`.

| Alan | Açıklama |
|---|---|
| resourceType | Enum seç |
| tier | `Basic` (Wood/Stone/Iron) veya `Advanced` (Steel/Gold/Crystal) |
| displayName | "Odun", "Taş" gibi |
| icon | UI'da görünecek sprite |
| weight | `Light` / `Medium` / `Heavy` (oyuncu taşıma kapasitesi) |
| worldPrefab | Yerde duran 3D model prefab'ı (önce **Bölüm 5** prefabını yap, sonra buraya sürükle) |

### 4.2 ShipData (`Assets/Data/Ships/`)
En az 4 adet: `Light`, `Medium`, `Heavy`, `Boss`.

| Alan | Tip |
|---|---|
| shipType | `ShipType` enum |
| displayName | "Yelkenli", "Galyon" vb. |
| maxHealth, moveSpeed, attackDamage, attackInterval | float (denge için sen ayarla) |
| prefab | Gemi prefab'ı (Bölüm 5.2) |

### 4.3 WaveData (`Assets/Data/Waves/`)
İlk 5-10 wave için birer asset. **Sonsuz wave sistemi var**: tanımlananın ötesinde son wave tekrar kullanılıyor (`WaveSpawner.GetWaveData:121`).

| Alan | |
|---|---|
| waveNumber | 1, 2, 3... |
| prepPhaseDuration | Bilgi amaçlı (asıl Prep süresi `GamePhaseController`'da hesaplanır) |
| ships | `WaveShipEntry[]` — her entry'de `shipData` + `count` |
| spawnInterval | Gemiler arası saniye |
| isBossWave | Wave 5, 10, 15... için true |

> Boss wave'ler `WaveScaler.IsBossWave` ile **wave % 5 == 0** kontrolüyle de otomatik tetiklenir; sen sadece doğru gemi sayısı/kompozisyonunu ver.

### 4.4 CaravanData (`Assets/Data/Caravans/`)
1-3 farklı kervan tipi yeter (zayıf/orta/zengin).

| Alan | |
|---|---|
| maxHealth | Kervan canı (haydut saldırısına dayanıklılık) |
| moveSpeed | float |
| cargo | `CaravanCargoEntry[]` — `resourceData` + `amount` |
| minWaveForAdvanced | Hangi wave'den sonra gelişmiş kaynak (Steel/Gold/Crystal) içermeye başlasın |
| prefab | Kervan prefab'ı |

### 4.5 BanditData (`Assets/Data/Bandits/`)
2 adet: `Raider` (hızlı zayıf), `Brute` (yavaş güçlü).

| Alan | |
|---|---|
| banditType | Enum |
| maxHealth, damage, moveSpeed | float |
| prefab | Haydut prefab'ı |

### 4.6 RecipeData (`Assets/Data/Recipes/`)
Kılıç ve kalkan tarifleri (ok/yay YOK). Örnek:
- Iron Sword: Iron×2 + Wood×1 → `outputWeapon = IronSword`, `requiredStationLevel = Tier1`
- Steel Sword: Steel×2 + Wood×1 → `requiredStationLevel = Tier2`
- Wooden Shield, Iron Shield, vb.

Kule/duvar craft'ı için de tarif: outputWeapon yerine `outputDefense` doldur.

### 4.7 DefenseData (`Assets/Data/Defenses/`)
`CannonTower`, `ArcherTower`, `Wall` için birer asset.

| Alan | |
|---|---|
| defenseType | Enum |
| damage, fireRate, range, splashRadius | float |
| maxHealth | Kule canı |

### 4.8 WeaponData (`Assets/Data/Weapons/`)
Kılıç ve kalkan çeşitleri.

### 4.9 PotionData (`Assets/Data/Potions/`)
`StrengthPotion` (saldırı +%50, 30sn) ve `HearingPotion` (proximity chat menzili ×2, 45sn). Süreleri `GameConstants`'tan al.

### 4.10 UpgradeData (`Assets/Data/Upgrades/`)
Her yükseltilebilir nesne için seviye geçişleri:
- CraftingStation Tier1→Tier2, Tier2→Tier3
- CannonTower / ArcherTower Tier1→Tier2, vb.
- Wheelbarrow yükseltmeleri
- Her birinde: `fromLevel`, `toLevel`, maliyet (kaynak listesi), `upgradeDuration`

### 4.11 WheelbarrowData (`Assets/Data/Wheelbarrow/`)
El arabası seviye verisi: kapasite, hız çarpanı.

---

## 5) Prefab'ları oluştur

> Prefab = sahnede bir GameObject hazırlayıp `Assets/Prefabs/...` klasörüne sürükle. Sahnedeki kopyayı sil; orijinal artık prefab.

### 5.1 Player prefab → `Assets/Prefabs/Player.prefab`

Boş GameObject oluştur, üzerine:
- **Tag**: `Player`
- **CharacterController** (Unity built-in)
- **PhotonView** + **PhotonTransformView** (Photon Pun)
- Scriptler:
  - `PlayerController`
  - `PlayerAnimator`
  - `PlayerHealth`
  - `PlayerCombat`
  - `PlayerInteraction`
  - `CarrySystem`
  - `WeaponManager`
  - `PotionSystem`
  - `RagdollController`
  - `PlayerSpawnController`
  - `PlayerNetSync`
- Child: 3D model + Animator (humanoid rig)
- Child: `Camera` (3rd person, `PlayerController.cameraTransform`'a bağla)
- Child: ragdoll bone hierarchy (Animator kapanınca RagdollController bunlara fizik veriyor)

**Inspector bağlamaları**: `PlayerAnimator` ve `PlayerSpawnController` üzerindeki tüm `[SerializeField]` referansları → kendi GameObject'inden Get-Component ile bağla (Inspector'da sürükle bırak).

### 5.2 Gemi prefab'ları → `Assets/Prefabs/Ships/`
Her gemi tipi için ayrı prefab (LightShip, MediumShip, HeavyShip, BossShip):
- **Tag**: `Enemy`
- Bileşenler: `ShipBase` (veya `BossShip` boss için), `ShipHealth`, `ShipMovement`, `ShipAttack`, `ShipDamageEffect`, `ShipSinkAnimation`, `ShipNetSync`
- `PhotonView` (sadece master client spawn ediyor ama view yine de gerekli)
- Bittiğinde her ShipData SO'sunun `prefab` alanına ilgili prefab'ı sürükle.

### 5.3 Kervan prefab → `Assets/Prefabs/Caravans/`
- **Tag**: `Caravan`
- Bileşenler: `CaravanController`, `CaravanMovement`, `CaravanNetSync`
- Yük noktası (kargo görseli için child empty)

### 5.4 Haydut prefab → `Assets/Prefabs/Bandits/`
- **Tag**: `Bandit`
- Bileşenler: `BanditAI`, `BanditHealth`, `BanditNetSync`, Animator, NavMeshAgent (BanditAI kullanıyorsa)

### 5.5 Kule prefab'ları → `Assets/Prefabs/Defenses/`
`CannonTower.prefab` ve `ArcherTower.prefab`:
- **Tag**: `Defense`
- Bileşenler: `CannonTower` / `ArcherTower`, `TowerAiming`, `TowerCameraRig`, `TowerUpgrade`, `TowerNetSync`
- Child: `aimPivot` (namlu/yay, dönen kısım)
- Child: `muzzle` (mermi spawn noktası, aimPivot altında)
- Child: `exitPoint` (oyuncu çıkış konumu)
- Child: kule kamerası (TowerCameraRig'e bağla, default kapalı)

**Projectile prefab**: ayrı prefab, `Projectile` scripti, `Rigidbody`, collider. `TowerController.projectilePrefab`'a sürükle.

### 5.6 Duvar prefab → `Assets/Prefabs/Defenses/Wall.prefab`
- **Tag**: `Defense` (veya `Castle` — duruma göre)
- Bileşenler: `Wall`, `WallRepair`
- 3 hasar aşaması child: `Stage_Intact`, `Stage_Cracked`, `Stage_Destroyed` (kod sırayla SetActive eder, `Wall.cs:90-107`)
- Üst yüzeyinin Layer'ı: `WallTop` (oyuncu sur üstünde yürüyebilsin)

### 5.7 El arabası prefab → kale içinde, oyuncular ittirebilsin
- Bileşenler: `WheelbarrowController` + `Rigidbody`

### 5.8 Loot prefab → `Assets/Prefabs/Resources/`
- **Tag**: `Loot`
- Her kaynak tipinin world görünüm prefab'ı (ResourceData.worldPrefab'a sürüklenecek)

### 5.9 Craft ocağı (sahnede, prefab şart değil)
- Bileşenler: `CraftingStation`, `CraftQueueManager`, `RecipeCatalog`, `UpgradeManager`
- `CraftingStation.recipes` array'ine tarif SO'larını sürükle
- `CraftingStation.upgrades` array'ine UpgradeData'ları sürükle

---

## 6) Lobby sahnesi → `Assets/Scenes/Lobby.unity`

`File → New Scene` → boş Scene yap → `Lobby` adıyla kaydet.

### GameObject'ler:

**`[Network]`** (boş GameObject):
- `NetworkManager` script
- `LobbyManager` script (kontrol: `gameSceneName = "Game"` olmalı)

**`[UI]`** (Canvas):
- Oda oluştur / katıl butonları (LobbyManager.CreateRoom / JoinRoom / QuickJoin'i çağıracak)
- Ready toggle (LobbyManager.SetReady)
- Bağlı oyuncu listesi (OnLobbyChanged event'i ile güncellenir)

**`[Camera]`**: Standart Main Camera + AudioListener.

### Build Settings'e ekle
`File → Build Settings → Add Open Scenes`. Lobby **index 0** olsun.

---

## 7) Game sahnesi → `Assets/Scenes/Game.unity`

Asıl oyun sahnesi. Build Settings'te **index 1** olmalı (LobbyManager bunu sahne adı `"Game"` ile yüklüyor).

### 7.1 Harita layout (3D)
- **Merkez (Kale içi)**: zemin (Layer `Ground`), craft ocağı GameObject, gözetleme kulesi (ortada), el arabası başlangıç noktası, oyuncu spawn noktaları (6 adet `Transform`).
- **Kuzey (Deniz)**: su (Layer `Water`), gemi spawn point'leri (4-6 adet Transform, kuzey kıyısına dağıt), sahil target noktası.
- **Doğu/Batı (Ticaret yolu)**: ağaçlık alan, kervan giriş/çıkış noktaları (her yön için Transform), pusu noktaları (3-5 adet, Tag `TradeRoad` olabilir).
- **Sur duvarları**: kale çevresine Wall prefab'larını yerleştir, üstleri `WallTop` layer.

### 7.2 Manager GameObject'leri

Boş GameObject'ler oluştur, her birine ilgili scripti ekle. Singleton hepsi — birer tane olmalı.

| GameObject | Scriptler | Inspector bağlama |
|---|---|---|
| `[Managers]` (parent) | — | Düzen için |
| ├─ `GamePhaseController` | `GamePhaseController` | — |
| ├─ `WaveManager` | `WaveManager` | — |
| ├─ `WaveSpawner` | `WaveSpawner`, `ShipFormation` | `waves[]` ← WaveData SO'ları, `spawnPoints[]` ← kuzey spawn Transform'ları, `shoreTarget` ← sahil Transform, `formation` ← aynı GameObject |
| ├─ `BanditSpawner` | `BanditSpawner` | `banditTypes[]` ← BanditData SO'ları, `ambushPoints[]` ← yol pusu Transform'ları |
| ├─ `CaravanSpawner` (yazılmadıysa scene loopback) | Spawn mantığı `GamePhaseController.CaravanDueThisPrep` üzerinden — basit spawner ekle | CaravanData + giriş/çıkış noktası |
| ├─ `EconomyManager` | `EconomyManager` | Başlangıç kaynakları (varsa) |
| ├─ `ObjectPooler` | `ObjectPooler` | — (kod runtime'da kullanıyor) |
| ├─ `ShipPool` | `ShipPool` | Prewarm gemi prefab'ları |
| ├─ `LootDistributor` | `LootDistributor` | — |
| ├─ `CastleHealth` | `CastleHealth` | (Wall'ları dinleyip toplam kale canını yönetir) |
| ├─ `WinLoseCondition` | `WinLoseCondition` | — |
| ├─ `GameRestartController` | `GameRestartController` | — |
| ├─ `UpgradeManager` | `UpgradeManager`, `UpgradeCatalog` | UpgradeData listesi |
| ├─ `RecipeCatalog` | `RecipeCatalog` | RecipeData listesi |
| ├─ `AuthorityManager` | `AuthorityManager` | — (master client / ownership) |
| ├─ `GameStateSync` | `GameStateSync` | PhotonView |
| ├─ `LateJoinSync` | `LateJoinSync` | PhotonView |
| └─ `CombatNetSync` / `CraftNetSync` | İlgili scriptler | PhotonView |

**Önemli**: Bu manager GameObject'inin **`DontDestroyOnLoad`** ile gelmesini istiyorsan, Lobby sahnesinde başlatıp Game'e taşınmasını sağla (NetworkManager zaten DontDestroyOnLoad yapıyor). Yoksa Game sahnesine direkt yerleştir.

### 7.3 UI Canvas

Canvas (Screen Space - Overlay) + EventSystem. Üzerine:
- `[WaveCounter]` → `WaveCounterUI`
- `[PhaseAnnouncer]` → `PhaseAnnouncerUI` (Prep/Wave geçişlerinde büyük yazı)
- `[BellSystem]` → `BellSystem` (haydut alarmı)
- `[BanditRaidUI]`, `[CaravanUI]`, `[LootPickupUI]`, `[PotionEffectUI]`, `[InteractPrompt]`, `[RecipeBoardUI]`, `[UpgradeProgressUI]`, `[DamageIndicatorUI]`
- `[DiegeticUIManager]` (parent — dünyada-yerleşik UI ögelerini yönetir)
- `[CameraShake]` → ana kameraya
- `[ProximityChatManager]` → Photon Voice ile entegre

Her UI scripti `OnEnable`'da EventBus'a subscribe oluyor, sahnede aktifse otomatik çalışır.

### 7.4 Player spawn

`PlayerSpawnController` her oyuncu prefab'ında. Sahnedeki spawn point'leri 6 adet `Transform` olarak parent altında topla; Photon LobbyManager → Game sahnesine geçiş yapınca her client `PhotonNetwork.Instantiate("Player", spawn[i].position, ...)` ile spawn'lar.

**Player prefab'ı `Assets/Resources/` klasörüne kopyala** (Photon `Instantiate` Resources'tan okur). Klasör yoksa oluştur.

### 7.5 Photon Voice (proximity chat)
- Sahneye `PhotonVoiceNetwork` GameObject ekle (Photon Voice paketinden).
- Player prefab'ına `Speaker` ve `Recorder` componentleri ekle.
- `ProximityChatManager` kodu ses menzilini `GameConstants.VOICE_BASE_RANGE` / `TOWER_VOICE_RANGE` üzerinden ayarlıyor.

### 7.6 Build Settings'e ekle
`Game` sahnesi index 1.

---

## 8) Test akışı

1. **Editor'da Play** → Lobby açılır → "QuickJoin" tıkla → oda oluşur → Ready ol → host olduğun için kendi başına Game'e geç.
2. **2 client testi**: Editor'da Play + Build alıp .exe çalıştır → ikisi de aynı odaya katılsın → herkes Ready → Game yüklensin.
3. Game sahnesinde:
   - Prep fazı başlamalı (UI'da timer + "Prep" yazısı)
   - Süre bitince Wave 1 başlar, kuzeyden gemiler gelir
   - Kale duvarına saldırırlar, can düşer (Wall hasar aşamaları görsel olarak değişmeli)
   - Oyuncu kuleye yaklaşıp E'ye basınca kuleye girer, sol tıkla ateş eder
   - Tüm gemiler ölünce Prep'e geri dönülür
   - Wave 3'te kervan gelmeli (CARAVAN_FIRST_WAVE = 3)
   - Wave 5'te boss gelmeli + ekstra prep süresi

### Beklenen kırılma noktaları
- **"NullReferenceException: ShipData prefab"** → SO'da prefab boş bırakılmış
- **"AmbushPoints empty"** → BanditSpawner Inspector'ına Transform array'i bağlanmamış
- **Oyuncu havada düşüyor / yere geçiyor** → zeminde collider yok ya da Layer yanlış
- **Photon "ServerSettings AppId is null"** → Bölüm 1'i tekrar yap
- **Kule girince ekran karışıyor** → TowerCameraRig'in default kapalı, sadece Enter'da açılması lazım

---

## 9) Dosya/script referansları (lokasyon haritası)

Bir şey ararken nereye bakacağın:

| Konu | Dosya |
|---|---|
| Tüm sabitler (HP, hız, tag, layer) | `Assets/Scripts/Shared/Constants/GameConstants.cs` |
| Photon RPC anahtarları | `Assets/Scripts/Shared/Constants/NetworkKeys.cs` |
| Sistemler arası event'ler | `Assets/Scripts/Shared/Events/EventBus.cs` |
| Bağlantı / oda | `Assets/Scripts/Network/NetworkManager.cs`, `LobbyManager.cs` |
| Faz / wave akışı | `Assets/Scripts/GameLoop/GamePhaseController.cs`, `WaveManager.cs` |
| Gemi spawn | `Assets/Scripts/Enemy/WaveSpawner.cs` |
| Haydut spawn | `Assets/Scripts/Enemy/BanditSpawner.cs` |
| Kervan | `Assets/Scripts/Enemy/CaravanController.cs` + `CaravanMovement.cs` |
| Oyuncu | `Assets/Scripts/Player/PlayerController.cs` (+ aynı klasör) |
| Kule | `Assets/Scripts/Defense/TowerController.cs` + alt sınıflar |
| Duvar | `Assets/Scripts/Defense/Wall.cs` |
| Craft | `Assets/Scripts/ResourceCraft/CraftingStation.cs` |
| Tüm SO tanımları | `Assets/Scripts/Shared/Data/*.cs` |

---

## 10) Önemli kurallar (kod tarafı)

Sahne/prefab yaparken bunları **bozma**:
- **EventBus üzerinden iletişim** — sistemler birbirini doğrudan referanslamıyor. UI hep `EventBus.OnXxx += ...` ile dinler.
- **`[SerializeField]` ile Inspector bağlama** — public field yok, bağlantıları Inspector'dan kuracaksın.
- **SO referansları Inspector'dan** — hardcode edilmemiş.
- **Tag/Layer isimleri GameConstants ile birebir** — değiştirme.
- **OnEnable subscribe / OnDisable unsubscribe** — bu kuralın gereği: prefab'ı disable edersen event subscription temizlenir, sorun olmaz.

---

## 11) İlerleme önerisi (sıra önemli)

Aşağıdaki sırayla git, her adımda bir önceki test edilmiş olmalı:

1. **Photon AppID + Tag/Layer** (Bölüm 1-2)
2. **SO'ları üret** (Bölüm 4) — prefab'ları boş bırak, geri dönüp dolduracaksın
3. **Boş prefab iskeletleri** (Bölüm 5) — sadece tag + script, görsel sonra
4. **SO'ların prefab alanlarını doldur** (geri dön Bölüm 4'e)
5. **Lobby sahnesi + Photon bağlantı testi** (Bölüm 6)
6. **Game sahnesi haritası ve manager'lar** (Bölüm 7)
7. **Player spawn testi** (tek başına gez)
8. **Wave 1 testi** (gemiler geliyor mu, ölünce wave bitiyor mu)
9. **Kule + craft testi**
10. **Kervan + haydut testi**
11. **Çok oyunculu test** (2 client, sonra 6)
12. **Görsel polish** (model/animasyon/efekt — son)

---

## 12) Yardım

Kod tarafıyla ilgili soru çıkarsa:
- `git log` ile commit geçmişine bak — her commit anlamlı bir mesajla atılmış (Türkçe).
- `Assets/Scripts/` altındaki dosyalarda her sınıfın başında ne yaptığını anlatan summary comment var.
- `graphify-out/GRAPH_REPORT.md` modül bağlantılarını gösteriyor (community / god node mantığı).

Bug bulursan kod sahibine (repo owner) ilet — Unity Editor tarafından kaynaklı mı, kod bug'ı mı ayırt etmesi gerek.
