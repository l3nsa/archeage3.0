using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using AAEmu.Commons.Cryptography;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Network.Login;
using AAEmu.Game.Core.Network.Stream;
using AAEmu.Game.Utils.Scripts;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

using NLog;

namespace AAEmu.Game
{
    public class GameService : IHostedService, IDisposable
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public static DateTime StartTime { get; set; }
        public static DateTime EndTime { get; set; }

        /// <summary>
        /// Runs a manager's Load() step. If the underlying compact.sqlite3 is missing a
        /// table/column or contains unexpected data, the failure is recorded in
        /// MissingData.log and the server keeps booting with that manager partially (or
        /// not at all) loaded. Dependent features will simply be unavailable until the
        /// data gap is filled.
        /// </summary>
        private static void SafeLoad(string name, Action load)
        {
            try
            {
                load();
            }
            catch (SqliteException ex)
            {
                _log.Error("{0}.Load() skipped due to SQLite error: {1}", name, ex.Message);
                MissingDataLogger.Instance.ReportSqlite(name, ex.Message, name + ".Load");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "{0}.Load() failed: {1}", name, ex.Message);
                MissingDataLogger.Instance.ReportGeneric("LOAD_FAILED", name + ": " + ex.GetType().Name + ": " + ex.Message, name + ".Load");
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _log.Info("Starting daemon: AAEmu.Game");

            // проверка чтения areasmission0.bai файла
            //NavigationSystem.ReadFromFile(@"g:\Games\Archeage1.2\game_0\main_world\paths\119_034\areasmission0.bai");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            #region Id Managers
            TaskIdManager.Instance.Initialize();
            TaskManager.Instance.Initialize();
            SafeLoad(nameof(LocalizationManager), () => LocalizationManager.Instance.Load());
            ObjectIdManager.Instance.Initialize();
            TradeIdManager.Instance.Initialize();
            #endregion

            #region Gameplay Managers
            ItemIdManager.Instance.Initialize();
            ChatManager.Instance.Initialize();
            CharacterIdManager.Instance.Initialize();
            FamilyIdManager.Instance.Initialize();
            ExpeditionIdManager.Instance.Initialize();
            VisitedSubZoneIdManager.Instance.Initialize();
            PrivateBookIdManager.Instance.Initialize();
            FriendIdManager.Instance.Initialize();
            MateIdManager.Instance.Initialize();
            HousingIdManager.Instance.Initialize();
            HousingTldManager.Instance.Initialize();
            TeamIdManager.Instance.Initialize();
            LaborPowerManager.Instance.Initialize();
            QuestIdManager.Instance.Initialize();

            ZoneManager.Instance.Load();
            WorldManager.Instance.Load();
            var heightmapTask = Task.Run(() =>
            {
                WorldManager.Instance.LoadHeightmaps();
            });
            SafeLoad(nameof(QuestManager), () => QuestManager.Instance.Load());

            SafeLoad(nameof(ShipyardManager), () => ShipyardManager.Instance.Load());

            SafeLoad(nameof(FormulaManager), () => FormulaManager.Instance.Load());
            SafeLoad(nameof(ExpirienceManager), () => ExpirienceManager.Instance.Load());
            SafeLoad(nameof(ConfigurationManager), () => ConfigurationManager.Instance.Load());

            TlIdManager.Instance.Initialize();
            SafeLoad(nameof(SpecialtyManager), () => SpecialtyManager.Instance.Load());
            SafeLoad(nameof(ItemManager), () => ItemManager.Instance.Load());
            SafeLoad(nameof(ItemManager) + ".LoadUserItems", () => ItemManager.Instance.LoadUserItems());
            SafeLoad(nameof(AnimationManager), () => AnimationManager.Instance.Load());
            SafeLoad(nameof(PlotManager), () => PlotManager.Instance.Load());
            SafeLoad(nameof(SkillManager), () => SkillManager.Instance.Load());
            SafeLoad(nameof(CraftManager), () => CraftManager.Instance.Load());
            SafeLoad(nameof(MateManager), () => MateManager.Instance.Load());
            SafeLoad(nameof(SlaveManager), () => SlaveManager.Instance.Load());
            SafeLoad(nameof(TeamManager), () => TeamManager.Instance.Load());
            SafeLoad(nameof(AuctionManager), () => AuctionManager.Instance.Load());
            SafeLoad(nameof(MailManager), () => MailManager.Instance.Load());

            SafeLoad(nameof(NameManager), () => NameManager.Instance.Load());
            SafeLoad(nameof(FactionManager), () => FactionManager.Instance.Load());
            SafeLoad(nameof(ExpeditionManager), () => ExpeditionManager.Instance.Load());
            SafeLoad(nameof(CharacterManager), () => CharacterManager.Instance.Load());
            SafeLoad(nameof(FamilyManager), () => FamilyManager.Instance.Load());
            SafeLoad(nameof(PortalManager), () => PortalManager.Instance.Load());
            SafeLoad(nameof(FriendMananger), () => FriendMananger.Instance.Load());

            SafeLoad(nameof(NpcManager), () => NpcManager.Instance.Load());
            SafeLoad(nameof(DoodadManager), () => DoodadManager.Instance.Load());
            SafeLoad(nameof(HousingManager), () => HousingManager.Instance.Load());
            SafeLoad(nameof(TransferManager), () => TransferManager.Instance.Load());
            SafeLoad(nameof(GimmickManager), () => GimmickManager.Instance.Load());

            await heightmapTask;

            SafeLoad(nameof(SpawnManager), () => SpawnManager.Instance.Load());
            try { SpawnManager.Instance.SpawnAll(); } catch (Exception ex) { _log.Error(ex, "SpawnManager.SpawnAll failed"); }
            try { HousingManager.Instance.SpawnAll(); } catch (Exception ex) { _log.Error(ex, "HousingManager.SpawnAll failed"); }
            //TransferManager.Instance.SpawnAll();
            #endregion

            #region Other Managers
            AccessLevelManager.Instance.Load();
            CashShopManager.Instance.Load();
            ScriptCompiler.Compile();

            SaveManager.Instance.Initialize();
            SpecialtyManager.Instance.Initialize();
            BoatPhysicsManager.Instance.Initialize();
            TransferManager.Instance.Initialize();
            GimmickManager.Instance.Initialize();
            SlaveManager.Instance.Initialize();

            //TransferManager.Instance.Initialize();
           
            EncryptionManager.Instance.Load();
            TimeManager.Instance.Start();
            TaskManager.Instance.Start();
            GameNetwork.Instance.Start();
            StreamNetwork.Instance.Start();
            LoginNetwork.Instance.Start();
            #endregion

            StartTime = DateTime.UtcNow;
            stopWatch.Stop();
            _log.Info("Server started! Took {0}", stopWatch.Elapsed);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _log.Info("Stopping daemon ...");

            SaveManager.Instance.Stop();

            SpawnManager.Instance.Stop();
            TaskManager.Instance.Stop();
            GameNetwork.Instance.Stop();
            StreamNetwork.Instance.Stop();
            LoginNetwork.Instance.Stop();

            /*
            HousingManager.Instance.Save();
            MailManager.Instance.Save();
            ItemManager.Instance.Save();
            */
            
            BoatPhysicsManager.Instance.Stop();
            GimmickManager.Instance.Stop();

            TimeManager.Instance.Stop();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _log.Info("Disposing ...");

            LogManager.Flush();
        }
    }
}
