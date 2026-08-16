using DynamicWin.LocalSend;
using DynamicWin.Main;
using DynamicWin.UI.Menu.Menus;
using DynamicWin.Utils;
using SkiaSharp;

namespace DynamicWin.UI.UIElements.Custom
{
    internal class LocalSendPanel : UIObject
    {
        readonly Tray tray;
        readonly List<DeviceRow> rows = new();
        int renderedVersion = -1;
        LocalSendSendStatus lastStatus = LocalSendSendStatus.Idle;
        float terminalTimer;
        const float TerminalHoldSeconds = 4f;

        DWText statusText;
        DWProgressBar progressBar;
        DWText progressLabel;
        DWTextButton cancelButton;

        const int MaxRows = 3;
        const float RowHeight = 22f;
        const float RowSpacing = 6f;
        // Row 1 sits 16px below the mode buttons. topContainer spans island
        // Y 15..45 with its buttons ending at Y 40; the strip starts at Y 57.5.
        const float FirstRowTopOffset = -1.5f;
        const float FirstRowCenterY = FirstRowTopOffset + RowHeight / 2;
        // Gap between the last row and the tray file list below the strip.
        const float BottomGap = 16f;

        // The strip hugs its actual content: 16px below the buttons, the rows,
        // then 16px above the tray file list.
        public static float RequiredStripHeight()
        {
            var count = Math.Max(1, Math.Min(LocalSendService.Instance?.Devices.Count ?? 0, MaxRows));
            var stack = count * RowHeight + (count - 1) * RowSpacing;
            return FirstRowTopOffset + stack + BottomGap;
        }

        public LocalSendPanel(UIObject? parent, Tray tray, Vec2 position, Vec2 size, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, size, alignment)
        {
            this.tray = tray;
            Color = Col.Transparent;

            statusText = new DWText(this, "Searching for nearby devices…", new Vec2(0, FirstRowCenterY), UIAlignment.TopCenter)
            {
                TextSize = 12,
                Color = Theme.TextSecond
            };
            AddLocalObject(statusText);

            progressLabel = new DWText(this, "", new Vec2(0, -18), UIAlignment.Center)
            {
                TextSize = 10,
                Color = Theme.TextMain
            };
            progressLabel.SilentSetActive(false);
            AddLocalObject(progressLabel);

            progressBar = new DWProgressBar(this, new Vec2(0, 2), new Vec2(160, 6), UIAlignment.Center);
            progressBar.value = 0;
            progressBar.SilentSetActive(false);
            AddLocalObject(progressBar);

            cancelButton = new DWTextButton(this, "Cancel", new Vec2(0, 26), new Vec2(70, 16), () => LocalSendService.Instance?.CancelSend(), UIAlignment.Center)
            {
                roundRadius = 15
            };
            cancelButton.SilentSetActive(false);
            AddLocalObject(cancelButton);
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            var service = LocalSendService.Instance;
            var state = service?.Status ?? LocalSendSendStatus.Idle;

            if (state is LocalSendSendStatus.Preparing or LocalSendSendStatus.Sending && service != null)
            {
                ClearRows();

                statusText.SilentSetActive(false);
                progressBar.SilentSetActive(true);
                progressLabel.SilentSetActive(true);
                cancelButton.SilentSetActive(state == LocalSendSendStatus.Sending);

                progressBar.value = service.Progress;
                progressLabel.Text = state == LocalSendSendStatus.Sending
                    ? DWText.Truncate(service.CurrentFile, 28)
                    : service.StatusMessage;

                lastStatus = state;
                return;
            }

            progressBar.SilentSetActive(false);
            cancelButton.SilentSetActive(false);
            progressLabel.SilentSetActive(false);

            if (lastStatus is LocalSendSendStatus.Preparing or LocalSendSendStatus.Sending
                && state is LocalSendSendStatus.Completed or LocalSendSendStatus.Failed
                    or LocalSendSendStatus.Rejected or LocalSendSendStatus.RequiresPin
                    or LocalSendSendStatus.Busy or LocalSendSendStatus.Cancelled)
            {
                Runtime.OpenOverlayMenu(new ToastMenu(service?.StatusMessage ?? "Transfer finished"), 3f);
            }
            lastStatus = state;

            if (service == null)
            {
                ClearRows();
                statusText.SilentSetActive(true);
                statusText.Text = "LocalSend is disabled in Settings";
                return;
            }

            if (state != LocalSendSendStatus.Idle)
            {
                if (state is LocalSendSendStatus.Completed or LocalSendSendStatus.Failed
                    or LocalSendSendStatus.Rejected or LocalSendSendStatus.RequiresPin
                    or LocalSendSendStatus.Busy or LocalSendSendStatus.Cancelled)
                {
                    // Let the user read the outcome, then fall back to the device list.
                    terminalTimer += deltaTime;
                    if (terminalTimer >= TerminalHoldSeconds)
                    {
                        terminalTimer = 0f;
                        service?.ResetState();
                    }
                }
                else terminalTimer = 0f;

                ClearRows();
                statusText.SilentSetActive(true);
                statusText.Text = DWText.Truncate(service.StatusMessage, 44);
                return;
            }

            terminalTimer = 0f;

            if (service.Registry.Version != renderedVersion)
            {
                renderedVersion = service.Registry.Version;
                RebuildRows(service);
            }

            statusText.SilentSetActive(rows.Count == 0);
            if (rows.Count == 0)
                statusText.Text = "Searching for nearby devices…";
        }

        void RebuildRows(LocalSendService service)
        {
            ClearRows();

            var rowWidth = Math.Max(Size.X - 40, 100);
            var devices = service.Devices;
            var count = Math.Min(devices.Count, MaxRows);

            for (var i = 0; i < count; i++)
            {
                var device = devices[i];
                var row = new DeviceRow(this, device, new Vec2(0, FirstRowCenterY + i * (RowHeight + RowSpacing)), new Vec2(rowWidth, RowHeight), () => SendToDevice(device), UIAlignment.TopCenter);
                rows.Add(row);
                AddLocalObject(row);
            }
        }

        void SendToDevice(LocalSendDevice device)
        {
            var files = tray.selectedFiles.Select(file => file.FileName).ToList();
            if (files.Count == 0)
            {
                Runtime.OpenOverlayMenu(new ToastMenu("Select files in the tray first"), 2.5f);
                return;
            }

            LocalSendService.Instance?.SendFiles(device, files);
        }

        void ClearRows()
        {
            rows.ForEach(DestroyLocalObject);
            rows.Clear();
        }
    }

    internal class DeviceRow : UIObject
    {
        readonly Action? clicked;

        public DeviceRow(UIObject? parent, LocalSendDevice device, Vec2 position, Vec2 size, Action onClick, UIAlignment alignment = UIAlignment.TopCenter) : base(parent, position, size, alignment)
        {
            clicked = onClick;
            roundRadius = 10;
            Color = Theme.Primary.Override(a: 0.08f);

            AddLocalObject(new DWText(this, DWText.Truncate(device.Identity.Alias, 16), new Vec2(12, 0), UIAlignment.MiddleLeft)
            {
                TextSize = 12,
                Color = Theme.TextMain,
                // Anchor the text's left edge at the aligned point; the default
                // 0.5 anchor centers the whole string on that point and lets long
                // aliases overflow the row.
                Anchor = new Vec2(0f, 0.5f)
            });

            AddLocalObject(new DWText(this, device.Address, new Vec2(-12, 0), UIAlignment.MiddleRight)
            {
                TextSize = 10,
                Color = Theme.TextThird,
                Anchor = new Vec2(1f, 0.5f)
            });
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            var target = IsHovering ? Theme.Primary.Override(a: 0.3f) : Theme.Primary.Override(a: 0.08f);
            Color = Col.Lerp(Color, GetColor(target), 10f * deltaTime);
        }

        public override void OnMouseUp()
        {
            clicked?.Invoke();
        }
    }
}
