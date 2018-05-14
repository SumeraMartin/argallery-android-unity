using UnityEngine.UI;
using System.IO;
using System.Collections;
using UnityEngine.SceneManagement;

namespace GoogleARCore.HelloAR
{
    using System.Collections.Generic;
    using GoogleARCore;
    using UnityEngine;
    using UnityEngine.Rendering;

#if UNITY_EDITOR
    using Input = InstantPreviewInput;
#endif
    public class MainController : MonoBehaviour
    {

		const float PICTURE_MAX_SIZE = 0.5f;

        public Camera FirstPersonCamera;

        public GameObject trackedPlanePrefab;

		public GameObject trackingPoint;

		public GameObject wall;

		public GameObject picture;

		public GameObject bottomSheetMenuForWallsPlacedState;

		public Button closeButton;

		public Button menuIcon;

		public Button wallsAddingCompleteButton;

		public Button anchorPicture;

		public Button releasePicture;

		public Button clickableOverlay;

		public Button togglWallsVisibilityButton;

		public Button screenOverlay;

		public List<Button> resetMenuButtons;

		public Text initialTrackingDescription;

		private List<TrackedPlane> newPlanes = new List<TrackedPlane>();

		private List<TrackedPlane> allPlanes = new List<TrackedPlane>();

		private bool isQuitting = false;

		private bool wasTracked = false;

		private bool isFirstPointAnchored = false;

		private bool isWallPlacingFinished = false;

		private bool isPictureAnchored = false;

		private List<GameObject> trackingPoints = new List<GameObject>();

		private List<GameObject> walls = new List<GameObject>();

		private List<BoxCollider> wallColliders = new List<BoxCollider>();

		private GameObject trackedPlane = null;

		private Texture2D localImage = null;

		private GameObject pictureCopy = null;

		private bool areWallsVisible = true;

		public void Awake() {
			var wallColider = wall.GetComponent<BoxCollider> ();
			wallColider.enabled = false;
			wallColliders.Add (wallColider);
			walls.Add (wall);

			screenOverlay.onClick.AddListener (delegate() {AddWallClicked ();});
			wallsAddingCompleteButton.onClick.AddListener (delegate() { WallsAdditionIsCompleted(); });
			anchorPicture.onClick.AddListener (delegate() { AnchorPicture(); });
			releasePicture.onClick.AddListener (delegate() { ReleasePicture(); });
			closeButton.onClick.AddListener (delegate() { CloseButtonClicked(); });
			menuIcon.onClick.AddListener (delegate() { MenuButtonClicked(); });
			clickableOverlay.onClick.AddListener (delegate() { DismissBottomSheets(); });
			togglWallsVisibilityButton.onClick.AddListener (delegate() { TogglWallsVisibilityClicked(); });
			resetMenuButtons.ForEach (resetButton => 
				resetButton.onClick.AddListener (delegate() {
					ResetButtonClicked ();
				})
			);
		}
			
        public void Update()
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

			if (localImage == null) {
				var filePath = "/data/data/com.sumera.argallery.unity/files/test.png";
				localImage = LoadPNG (filePath);
				var rawImage = picture.GetComponentInChildren<RawImage> ();
				rawImage.texture = localImage;
				var pictureSize = CalculateSizeOfPicture(localImage);
				picture.transform.localScale = new Vector3 (picture.transform.localScale.x * pictureSize.x, picture.transform.localScale.y, picture.transform.localScale.z * pictureSize.y);
			}

			if (pictureCopy == null) {
				pictureCopy = Instantiate (picture);
			}
				
            QuitOnConnectionErrors();

			if (!isFirstPointAnchored) {
				wallsAddingCompleteButton.gameObject.transform.parent.gameObject.SetActive(false);
				anchorPicture.gameObject.transform.parent.gameObject.SetActive(false);
				releasePicture.gameObject.transform.parent.gameObject.SetActive(false);
				wall.gameObject.SetActive (false);
			}

			if (isFirstPointAnchored && !isWallPlacingFinished) {
				wallsAddingCompleteButton.gameObject.transform.parent.gameObject.SetActive(true);
				anchorPicture.gameObject.transform.parent.gameObject.SetActive(false);
				releasePicture.gameObject.transform.parent.gameObject.SetActive(false);
				wall.gameObject.SetActive (true);
			}

			if (isFirstPointAnchored && isWallPlacingFinished) {
				wallsAddingCompleteButton.gameObject.transform.parent.gameObject.SetActive(false);

				if (isPictureAnchored) {
					anchorPicture.gameObject.transform.parent.gameObject.SetActive(false);
					releasePicture.gameObject.transform.parent.gameObject.SetActive(true);
				} else {
					anchorPicture.gameObject.transform.parent.gameObject.SetActive(true);
					releasePicture.gameObject.transform.parent.gameObject.SetActive(false);
				}
			}
				
            if (Session.Status != SessionStatus.Tracking)
            {
                const int lostTrackingSleepTimeout = 15;
                Screen.sleepTimeout = lostTrackingSleepTimeout;
                if (!isQuitting && Session.Status.IsValid())
                {
					initialTrackingDescription.enabled = true;
					SetWallsActive (false);
					SetPictureActive (false);
					wallsAddingCompleteButton.gameObject.transform.parent.gameObject.SetActive(false);
					anchorPicture.gameObject.transform.parent.gameObject.SetActive(false);
					releasePicture.gameObject.transform.parent.gameObject.SetActive(false);
                }

                return;
            }
				
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Session.GetTrackables<TrackedPlane>(newPlanes, TrackableQueryFilter.All);
            for (int i = 0; i < newPlanes.Count; i++)
            {
				if (!wasTracked) {
					wasTracked = true;
					GameObject planeObject = Instantiate(trackedPlanePrefab, Vector3.zero, Quaternion.identity, transform);
					planeObject.GetComponent<TrackedPlaneVisualizer> ().Initialize (newPlanes [i]);
					trackedPlane = planeObject;
					Session.CreateAnchor (newPlanes [i].CenterPose, newPlanes [i]);
				}     
            }

			if (wasTracked && !isWallPlacingFinished) {
				screenOverlay.gameObject.SetActive (true);
			} else {
				screenOverlay.gameObject.SetActive (false);
			}
				
            Session.GetTrackables<TrackedPlane>(allPlanes);
            bool showSearchingUI = true;
            for (int i = 0; i < allPlanes.Count; i++)
            {
                if (allPlanes[i].TrackingState == TrackingState.Tracking)
                {
                    showSearchingUI = false;
                    break;
                }
            }
				
			SetWallsActive (!showSearchingUI && areWallsVisible);
			SetPictureActive (!showSearchingUI);
			initialTrackingDescription.enabled = showSearchingUI;

			if (isFirstPointAnchored && !isWallPlacingFinished) {
				var lastPointsPosition = trackingPoints[trackingPoints.Count - 1].transform.position;
				var trackingPointPosition = new Vector3(trackingPoint.transform.position.x, lastPointsPosition.y, trackingPoint.transform.position.z);
				var newPosition = (lastPointsPosition + trackingPointPosition) / 2;
				wall.transform.position = newPosition;
				wall.transform.LookAt (new Vector3(trackingPoint.transform.position.x, lastPointsPosition.y, trackingPoint.transform.position.z));
				var wallWidth = Vector3.Distance(lastPointsPosition, trackingPointPosition);
				wall.transform.localScale = new Vector3 (0.001f, 5f, wallWidth);
				wall.transform.position += new Vector3 (0, 2.5f, 0);
				wallColliders.ForEach ((collider) => collider.enabled = false);
			} 

			if (isWallPlacingFinished && !isPictureAnchored) {
				wallColliders.ForEach ((collider) => collider.enabled = true);

				trackingPoint.gameObject.SetActive(false);

				var pictureRaycast = FirstPersonCamera.ViewportPointToRay (new Vector3 (0.5f, 0.5f, 0f));
				RaycastHit pictureHit;
				if (Physics.Raycast (pictureRaycast, out pictureHit)) {
					if (pictureHit.transform.tag == "wall") {
						var hitRotation = pictureHit.transform.rotation.eulerAngles;
						var pictureRotation = picture.transform.rotation;

						var firstPicturePosition = new Vector3(pictureHit.point.x, pictureHit.point.y, pictureHit.point.z);
						picture.transform.rotation = Quaternion.Euler (90, hitRotation.y - 90, pictureRotation.z);
						picture.transform.position = firstPicturePosition;
						picture.transform.Translate (picture.transform.forward * 0.01f);

						var secondPicturePosition = new Vector3(pictureHit.point.x, pictureHit.point.y, pictureHit.point.z);
						pictureCopy.transform.rotation = Quaternion.Euler (90, hitRotation.y + 90, pictureRotation.z);
						pictureCopy.transform.position = secondPicturePosition;
						pictureCopy.transform.Translate (pictureCopy.transform.forward * 0.01f);

						var cameraPosition = FirstPersonCamera.transform.position;
						var firstPictureDistance = Vector3.Distance (cameraPosition, picture.transform.position);
						var secondPictureDistance = Vector3.Distance (cameraPosition, pictureCopy.transform.position);

						if (firstPictureDistance <= secondPictureDistance) {
							picture.GetComponent<Renderer>().enabled = true;
							pictureCopy.GetComponent<Renderer>().enabled = false;
						} else {
							picture.GetComponent<Renderer>().enabled = false;
							pictureCopy.GetComponent<Renderer>().enabled = true;
						}
					}
				}
			}
				
			Ray raycast = FirstPersonCamera.ViewportPointToRay (new Vector3 (0.5f, 0.5f, 0f));
			RaycastHit raycastHit;
			if (Physics.Raycast(raycast, out raycastHit)) {
				if (raycastHit.transform.tag == "floor") {
					trackingPoint.transform.position = raycastHit.point;
				}
			}
        }
			
        private void QuitOnConnectionErrors() {
            if (isQuitting) {
                return;
            }
				
			if (Session.Status == SessionStatus.ErrorApkNotAvailable) {
				ShowAndroidToastMessage("ARCore is not supported");
				isQuitting = true;
				Invoke("_DoQuit", 0.5f);
			}
            else if (Session.Status == SessionStatus.ErrorPermissionNotGranted) {
                ShowAndroidToastMessage("Camera permission is needed to run this application.");
                isQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError()) {
                ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
                isQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        private void DoQuit() {
            Application.Quit();
        }
			
        private void ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                        message, 0);
                    toastObject.Call("show");
                }));
            }
        }

		private void WallsAdditionIsCompleted() {
			TogglWallsVisibilityClicked ();
			isWallPlacingFinished = true;
		}

		private void ReleasePicture() {
			isPictureAnchored = false;
		}

		private void AnchorPicture() {
			isPictureAnchored = true;
		}

		private void CloseButtonClicked() {
			if (!isQuitting) {
				isQuitting = true;
				Invoke("_DoQuit", 0.5f);
			}
		}
			
		private void MenuButtonClicked() {
			if (bottomSheetMenuForWallsPlacedState.activeSelf) {
				bottomSheetMenuForWallsPlacedState.SetActive (false);
				clickableOverlay.gameObject.SetActive (false);
			} else {
				bottomSheetMenuForWallsPlacedState.SetActive (true);
				clickableOverlay.gameObject.SetActive (true);
			}
		}

		private void DismissBottomSheets() {
			bottomSheetMenuForWallsPlacedState.SetActive (false);
			clickableOverlay.gameObject.SetActive (false);
		}

		private void ResetButtonClicked() {			
			SceneManager.LoadScene( SceneManager.GetActiveScene().name, LoadSceneMode.Single);
		}

		private void AddWallClicked() {
			var trackingPointCopy = Instantiate (trackingPoint, gameObject.transform.parent);
			trackingPoints.Add (trackingPointCopy);

			isFirstPointAnchored = true;

			var wallCopy = Instantiate (wall, wall.transform.parent);
			walls.Add (wallCopy);

			var wallColider = wallCopy.GetComponent<BoxCollider> ();
			wallColider.enabled = false;
			wallColliders.Add (wallColider);
		}

		private void TogglWallsVisibilityClicked() {
			areWallsVisible = !areWallsVisible;
			DismissBottomSheets ();
		}

		private void SetWallsActive(bool isActive) {
			var walls = GameObject.FindGameObjectsWithTag("wall");
			foreach (var wall in walls) {
				var renderer = wall.GetComponent<Renderer> ();
				renderer.enabled = isActive;
			}

			var trackingPoints = GameObject.FindGameObjectsWithTag("trackingPoint");
			foreach (var trackingPoint in trackingPoints) {
				var renderer = trackingPoint.GetComponent<Renderer> ();
				renderer.enabled = isActive;
			}

		}

		private void SetPictureActive(bool isActive) {
			picture.SetActive (isActive);
			pictureCopy.SetActive (isActive);
		}

		private Vector2 CalculateSizeOfPicture(Texture2D texture) {
			if (texture.width >= texture.height) {
				var scale = 1f / texture.width;
				var newWidth = texture.width / 1f / texture.width;
				var newHeight = texture.height / 1f / texture.width;
				return new Vector2 (newWidth, newHeight);
			} else {
				var scale = 1f / texture.height;
				var newWidth = texture.width / 1f / texture.height;
				var newHeight = texture.height / 1f / texture.height;
				return new Vector2 (newWidth, newHeight);
			}
		}
			
		private Texture2D LoadPNG(string filePath) {
			if (File.Exists (filePath)) {
				var fileData = File.ReadAllBytes (filePath);
				var texture = new Texture2D (2, 2);
				texture.LoadImage (fileData);
				return texture;
			} else {
				Debug.LogError ("File not found: " + filePath);
			}
			return null;
		}
    }
}
