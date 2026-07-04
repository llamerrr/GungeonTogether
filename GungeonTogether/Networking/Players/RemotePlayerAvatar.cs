using UnityEngine;

namespace GungeonTogether.Networking
{
    public class RemotePlayerAvatar : MonoBehaviour
    {
        private tk2dSprite _sprite;
        private Vector3 _targetPosition;
        private bool _hasTarget;

        public static RemotePlayerAvatar Create(ulong steamId, Vector2 position, float rotation)
        {
            GameObject go = new GameObject("RemotePlayer_" + steamId);
            RemotePlayerAvatar avatar = go.AddComponent<RemotePlayerAvatar>();
            avatar.Initialise(position, rotation);
            return avatar;
        }

        private void Initialise(Vector2 position, float rotation)
        {
            transform.position = new Vector3(position.x, position.y, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            _targetPosition = transform.position;
            _hasTarget = true;

            PlayerController localPlayer = GetLocalPlayer();
            tk2dBaseSprite sourceSprite = localPlayer != null ? localPlayer.sprite : null;
            if (sourceSprite == null)
            {
                Debug.LogWarning("[RemotePlayer] Could not find local player sprite to copy.");
                return;
            }

            _sprite = gameObject.AddComponent<tk2dSprite>();
            _sprite.SetSprite(sourceSprite.Collection, sourceSprite.spriteId);
            _sprite.HeightOffGround = sourceSprite.HeightOffGround;
            _sprite.SortingOrder = sourceSprite.SortingOrder;
            _sprite.FlipX = sourceSprite.FlipX;
            _sprite.color = new Color(0.55f, 0.95f, 1f, 1f);
        }

        public void Apply(Vector2 position, float rotation, int spriteId, bool flipX)
        {
            _targetPosition = new Vector3(position.x, position.y, 0f);
            _hasTarget = true;
            transform.rotation = Quaternion.Euler(0f, 0f, rotation);

            if (_sprite != null)
            {
                if (spriteId >= 0 && _sprite.spriteId != spriteId)
                {
                    _sprite.SetSprite(spriteId);
                }

                _sprite.FlipX = flipX;
            }
        }

        private void Update()
        {
            if (!_hasTarget) return;

            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * 18f);
        }

        private static PlayerController GetLocalPlayer()
        {
            GameManager gameManager = Object.FindObjectOfType<GameManager>();
            return gameManager != null ? gameManager.PrimaryPlayer : null;
        }
    }
}
