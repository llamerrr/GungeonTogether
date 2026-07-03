using UnityEngine;
using GungeonTogether.Networking;
using GungeonTogether.Networking.Steam;
using GungeonTogether.Systems.Logging;
using Debug = GungeonTogether.Systems.Logging.Debug;

namespace GungeonTogether.UI
{
	public static class UIManager
	{
		private static GameObject _root;
		private static dfPanel _panel;
		private static dfLabel _statusLabel;
		private static dfButton _hostButton;
		private static dfButton _inviteButton;
		private static dfButton _leaveButton;

		public static void Initialise()
		{
			// Lazy: build only when in foyer and UI system exists.
		}

		public static void Update()
		{
			try
			{
				GameManager gameManager = Object.FindObjectOfType<GameManager>();
				if (gameManager == null) return;
				if (!gameManager.IsFoyer)
				{
					SetVisible(false);
					return;
				}

				EnsureBuilt();
				UpdateStatus();
			}
			catch { }
		}

		private static void EnsureBuilt()
		{
			if (_panel != null) return;
			GameUIRoot uiRoot = Object.FindObjectOfType<GameUIRoot>();
			if (uiRoot == null) return;
			if (uiRoot.Manager == null) return;

			dfGUIManager gui = uiRoot.Manager;

			// Find a button template for consistent styling.
			dfButton template = null;
			var mm = Object.FindObjectOfType<MainMenuFoyerController>();
			if (mm != null && mm.NewGameButton != null)
			{
				template = mm.NewGameButton;
			}
			if (template == null)
			{
				Debug.LogWarning("[UI] Could not find MainMenuFoyerController.NewGameButton as template.");
			}

			_root = new GameObject("GungeonTogether_UI");
			_root.transform.parent = gui.transform;
			_root.transform.localPosition = Vector3.zero;
			_root.transform.localScale = Vector3.one;

			_panel = _root.AddComponent<dfPanel>();
			_panel.Anchor = dfAnchorStyle.Top | dfAnchorStyle.Left;
			_panel.RelativePosition = new Vector3(25f, 25f, 0f);
			_panel.Width = 420f;
			_panel.Height = 300f;
			_panel.IsVisible = true;
			_panel.Opacity = 1f;

			Debug.Log($"[UI] Panel created: Position={_panel.RelativePosition}, Size={_panel.Width}x{_panel.Height}");

			if (template != null)
			{
				_panel.Atlas = template.Atlas;
				Debug.Log($"[UI] Panel atlas set from template");
			}

			_statusLabel = CreateLabel(gui, _panel, template);
			_statusLabel.RelativePosition = new Vector3(10f, 10f, 0f);
			_statusLabel.Width = _panel.Width - 20f;
			_statusLabel.Height = 70f;
			_statusLabel.VerticalAlignment = dfVerticalAlignment.Top;
			_statusLabel.TextScale = 1.0f;
			_statusLabel.ProcessMarkup = true;
			_statusLabel.ColorizeSymbols = true;

			Debug.Log($"[UI] Status label created: Position={_statusLabel.RelativePosition}, Size={_statusLabel.Width}x{_statusLabel.Height}");

			// Layout buttons vertically within the panel
			float currentY = _statusLabel.RelativePosition.y + _statusLabel.Height + 10f;
			float buttonWidth = (_panel.Width - 30f) / 2f;
			float buttonHeight = 40f;
			float buttonSpacing = 10f;

			Debug.Log($"[UI] Button layout: currentY={currentY}, buttonWidth={buttonWidth}, buttonHeight={buttonHeight}, spacing={buttonSpacing}");

		_hostButton = CreateButtonFromTemplate(gui, _panel, template, "GT_HostButton", "HOST LOBBY", 10f, currentY, buttonWidth, buttonHeight);
		_hostButton.Click += OnHostClicked;
		Debug.Log($"[UI] Host button created: Position={_hostButton.RelativePosition}, Size={_hostButton.Width}x{_hostButton.Height}, Visible={_hostButton.IsVisible}");

		_inviteButton = CreateButtonFromTemplate(gui, _panel, template, "GT_InviteButton", "INVITE", 10f + buttonWidth + buttonSpacing, currentY, buttonWidth, buttonHeight);
		_inviteButton.Click += OnInviteClicked;
		Debug.Log($"[UI] Invite button created: Position={_inviteButton.RelativePosition}, Size={_inviteButton.Width}x{_inviteButton.Height}, Visible={_inviteButton.IsVisible}");

		currentY += buttonHeight + buttonSpacing;

		_leaveButton = CreateButtonFromTemplate(gui, _panel, template, "GT_LeaveButton", "LEAVE", 10f, currentY, buttonWidth, buttonHeight);
		_leaveButton.Click += OnLeaveClicked;
		}

		private static void UpdateStatus()
		{
			if (_statusLabel == null) return;

			var lobby = SteamLobbyManager.Instance;
			string lobbyText = lobby.IsInLobby ? ("Lobby: " + lobby.CurrentLobbyId) : "Lobby: (none)";
			string roleText = NetworkManager.Instance.IsHost ? "Role: Host" : (NetworkManager.Instance.IsClient ? "Role: Client" : "Role: (none)");
			string connText = NetworkManager.Instance.IsConnected ? "Net: Connected" : "Net: Disconnected";

			_statusLabel.ModifyLocalizedText("GUNGEON TOGETHER\n" + lobbyText + "\n" + roleText + " | " + connText);

			// Button enable states
			if (_hostButton != null) _hostButton.IsEnabled = !NetworkManager.Instance.IsConnected;
			if (_inviteButton != null) _inviteButton.IsEnabled = lobby.IsInLobby;
			if (_leaveButton != null) _leaveButton.IsEnabled = lobby.IsInLobby || NetworkManager.Instance.IsConnected;
		}

		private static void SetVisible(bool visible)
		{
			if (_panel != null) _panel.IsVisible = visible;
		}

		private static void OnHostClicked(dfControl control, dfMouseEventArgs mouseEvent)
		{
			Debug.Log("[UI] Host Lobby clicked.");
			SteamLobbyManager.Instance.CreateLobby(4);
		}

		private static void OnInviteClicked(dfControl control, dfMouseEventArgs mouseEvent)
		{
			Debug.Log("[UI] Invite clicked.");
			SteamLobbyManager.Instance.OpenInviteDialog();
		}

		private static void OnLeaveClicked(dfControl control, dfMouseEventArgs mouseEvent)
		{
			Debug.Log("[UI] Leave clicked.");
			SteamLobbyManager.Instance.LeaveLobby();
			NetworkManager.Instance.Shutdown();
		}

		private static dfLabel CreateLabel(dfGUIManager gui, dfControl parent, dfButton template)
		{
			GameObject go = new GameObject("GT_StatusLabel");
			go.transform.parent = parent.transform;
			go.transform.localScale = Vector3.one;
			var lbl = go.AddComponent<dfLabel>();
			if (template != null)
			{
				lbl.Atlas = template.Atlas;
				lbl.Font = template.Font;
				lbl.TextScale = template.TextScale;
				lbl.Color = template.TextColor;
			}
			else
			{
				lbl.Color = Color.white;
			}
			lbl.WordWrap = true;
			lbl.IsVisible = true;
			return lbl;
		}

		private static dfButton CreateButtonFromTemplate(dfGUIManager gui, dfControl parent, dfButton template, string name, string text, float posX, float posY, float width, float height)
		{
			// Create button from scratch instead of cloning to avoid parent hierarchy issues
			GameObject go = new GameObject(name);
			go.transform.parent = parent.transform;
			go.transform.localScale = Vector3.one;
			go.transform.localPosition = Vector3.zero;

			dfButton btn = go.AddComponent<dfButton>();
			
			// Apply styling from template if available
			if (template != null)
			{
				btn.Atlas = template.Atlas;
				btn.Font = template.Font;
				btn.TextScale = template.TextScale;
				btn.TextColor = template.TextColor;
				btn.BackgroundSprite = template.BackgroundSprite;
				btn.FocusSprite = template.FocusSprite;
				btn.HoverSprite = template.HoverSprite;
				btn.PressedSprite = template.PressedSprite;
				btn.DisabledSprite = template.DisabledSprite;
			}
			
			btn.Text = text;
			btn.forceUpperCase = true;
			btn.IsInteractive = true;
			btn.IsVisible = true;
			btn.IsEnabled = true;
			
			// Set position and size
			btn.RelativePosition = new Vector3(posX, posY, 0f);
			btn.Width = width;
			btn.Height = height;
			
			Debug.Log($"[UI] Button '{name}' created from scratch: Position={btn.RelativePosition}, Size={btn.Width}x{btn.Height}");
			
			return btn;
		}
	}
}

