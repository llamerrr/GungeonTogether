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
				if (GameManager.Instance == null) return;
				if (!GameManager.Instance.IsFoyer)
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
			if (GameUIRoot.Instance == null) return;
			if (GameUIRoot.Instance.Manager == null) return;

			dfGUIManager gui = GameUIRoot.Instance.Manager;

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
			_panel.Height = 165f;
			_panel.IsVisible = true;
			_panel.Opacity = 1f;

			if (template != null)
			{
				_panel.Atlas = template.Atlas;
			}

			_statusLabel = CreateLabel(gui, _panel, template);
			_statusLabel.RelativePosition = new Vector3(10f, 10f, 0f);
			_statusLabel.Width = _panel.Width - 20f;
			_statusLabel.Height = 55f;
			_statusLabel.VerticalAlignment = dfVerticalAlignment.Top;
			_statusLabel.TextScale = 1.0f;
			_statusLabel.ProcessMarkup = true;
			_statusLabel.ColorizeSymbols = true;

			_hostButton = CreateButtonFromTemplate(gui, _panel, template, "GT_HostButton", "HOST LOBBY");
			_hostButton.RelativePosition = new Vector3(10f, 350f, 0f);
			_hostButton.Click += OnHostClicked;

			_inviteButton = CreateButtonFromTemplate(gui, _panel, template, "GT_InviteButton", "INVITE (STEAM)");
			_inviteButton.RelativePosition = new Vector3(10f, 450f, 0f);
			_inviteButton.Click += OnInviteClicked;

			_leaveButton = CreateButtonFromTemplate(gui, _panel, template, "GT_LeaveButton", "LEAVE");
			_leaveButton.RelativePosition = new Vector3(10f, 550f, 0f);
			_leaveButton.Click += OnLeaveClicked;

			_hostButton.Width = 400f;
			_inviteButton.Width = 200f;
			_leaveButton.Width = 180f;
			_hostButton.Height = 35f;
			_inviteButton.Height = 35f;
			_leaveButton.Height = 35f;

			Debug.Log("[UI] GungeonTogether foyer panel created.");
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

		private static dfButton CreateButtonFromTemplate(dfGUIManager gui, dfControl parent, dfButton template, string name, string text)
		{
			dfButton btn;

			if (template != null)
			{
				GameObject clone = Object.Instantiate(template.gameObject);
				clone.name = name;
				clone.transform.parent = parent.transform;
				clone.transform.localScale = Vector3.one;

				btn = clone.GetComponent<dfButton>();

				// Remove key navigation to avoid interfering with base menu.
				var keys = clone.GetComponent<UIKeyControls>();
				if (keys != null) Object.Destroy(keys);

				btn.IsInteractive = true;
				btn.IsVisible = true;
				btn.IsEnabled = true;
				btn.ModifyLocalizedText(text);
			}
			else
			{
				GameObject go = new GameObject(name);
				go.transform.parent = parent.transform;
				go.transform.localScale = Vector3.one;
				btn = go.AddComponent<dfButton>();
				btn.IsInteractive = true;
				btn.IsVisible = true;
				btn.IsEnabled = true;
				btn.Text = text;
			}

			btn.forceUpperCase = true;
			btn.ModifyLocalizedText(text.ToUpperInvariant());
			return btn;
		}
	}
}

