using System;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace TimedShutdownPlugin
{
    [ApiVersion(2, 1)]
    public class TimedShutdown : TerrariaPlugin
    {
        public override string Name => "Timed Shutdown";
        public override string Author => "Developer";
        public override string Description => "Выключает сервер с таймером в 60 секунд и сохранением карты.";
        public override Version Version => new Version(1, 0, 0);

        private bool _isShuttingDown = false;
        private bool _shouldSaveAndQuit = false;

        public TimedShutdown(Main game) : base(game) { }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("tshock.maintenance", TShutdownCommand, "tshutdown"));
            
            // Подписываемся на обновление игры. Этот хук ВСЕГДА работает в главном потоке.
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
        }

        private void TShutdownCommand(CommandArgs args)
        {
            if (_isShuttingDown)
            {
                args.Player.SendErrorMessage("Сервер уже находится в процессе выключения!");
                return;
            }

            _isShuttingDown = true;
            Task.Run(() => StartShutdownSequence(60));
        }

        private async Task StartShutdownSequence(int seconds)
        {
            while (seconds > 0)
            {
                if (seconds == 60 || seconds == 30 || seconds == 10 || seconds <= 5)
                {
                    TSPlayer.All.SendMessage($"[Сервер] Выключение через {seconds} сек. Пожалуйста, завершите свои дела!", Microsoft.Xna.Framework.Color.Crimson);
                }

                await Task.Delay(1000);
                seconds--;
            }

            TSPlayer.All.SendMessage("[Сервер] Сохранение карты...", Microsoft.Xna.Framework.Color.Orange);

            // Вместо вызова методов потока просто даем сигнал хуку GameUpdate
            _shouldSaveAndQuit = true;
        }

        // Этот метод вызывается внутри главного потока Terraria множество раз в секунду
        private void OnGameUpdate(EventArgs args)
        {
            // Если сигнал на выключение не поступал — ничего не делаем
            if (!_shouldSaveAndQuit) return;

            // Сразу сбрасываем флаг, чтобы код не вызвался повторно на следующем тике
            _shouldSaveAndQuit = false;

            try
            {
                // Мы внутри главного потока, тут сохранение абсолютно безопасно
                Terraria.IO.WorldFile.SaveWorld(true);
                TSPlayer.All.SendMessage("[Сервер] Карта успешно сохранена. Выключение...", Microsoft.Xna.Framework.Color.LimeGreen);
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Ошибка при сохранении мира: {ex}");
            }
            finally
            {
                // Отключаем сервер
                Netplay.Disconnect = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Обязательно отписываемся от хука при выгрузке плагина
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            }
            base.Dispose(disposing);
        }
    }
}
