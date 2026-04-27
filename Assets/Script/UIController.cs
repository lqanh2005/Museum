using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class UIController : MonoBehaviour
{
    public static UIController instance;

    public PlayerController player;

    public GameObject tooltip;


    public QuizUIController quizUIController;
    public ContentController contentController;
    public TooltipUI tooltipUI;
    public Joystick joystick;
    Color32 activeColor = new Color32(5, 118, 100, 100);
    Color32 inactiveColor = new Color32(5, 118, 100, 0);
    [Header("BottomPanel")]
    public Button btnChoice;
    public Button btnMove;
    public Image imgBtn;
    public Button btnPlant, btnAnimal, btnNhanSo, btnThanhTuu;
    public GameObject bottomPanel;
    public Button btnNext, btnBack;
    public TMP_Text number;

    public Button btnClose;
    public GameObject panelTut;

    public Button btnClose_1;
    public GameObject panelIntro;

    [Header("ThanhTuu")]
    public Image imgThanhTuu;
    public Button nextBtn;
    public Button closeBtn;
    public List<Sprite> thanhTuuSprites;

    [Header("Button Popup")]
    public GameObject buttonPopup;
    public Button playVidBtn;
    public Button detailBtn;
    public Button questBtn;

    [Header("Video (StreamingAssets)")]
    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("Nếu có, sẽ bật khi bấm phát video.")]
    [SerializeField] private GameObject videoPanel;
    [Tooltip("Hiển thị khi phát video; tắt khi bấm Btn Close Raw Image.")]
    public RawImage rawImage;
    public Button btnCloseRawImage;

    [Header("View Full")]
    public Image viewFull;
    public List<Sprite> viewFullSprites;
    public Button viewFullBtn;
    public Button closeViewBtn;
    enum PopupSource { None = 0, Plant = 1, Animal = 2, NhanSo = 3 }
    PopupSource _popupSource;
    int _thanhTuuSpriteIndex;

    public PathType currentPathType;

    // Biến để quản lý tooltip navigation
    private List<ClickAble> currentClickableList = new List<ClickAble>();
    private int currentTooltipIndex = 0;
    private bool isNavigatingTooltips = false;
    private ClickAble currentBlinkingObject = null; // Object đang nhấp nháy
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        tooltipUI.Init();
    }

    private void Start()
    {
        panelTut.SetActive(true);
        btnClose.onClick.AddListener(() =>
        {
            panelTut.SetActive(false);
        });
        panelIntro.SetActive(true);
        btnClose_1.onClick.AddListener(() =>
        {
            panelIntro.SetActive(false);
        });
        btnMove.onClick.AddListener(() =>
        {
            imgBtn.gameObject.SetActive(false);
            bottomPanel.SetActive(false);
            joystick.gameObject.SetActive(true);
            player.isPlant = false;
            player.isNhanSo = false;
            player.isAnimal = false;
        });
        btnChoice.onClick.AddListener(() =>
        {
            imgBtn.gameObject.SetActive(!imgBtn.gameObject.activeSelf);
            if(imgBtn.gameObject.activeSelf == false)
            {
                player.isPlant = false;
                player.isNhanSo = false;
                player.isAnimal = false;
                player.moveJoystick.gameObject.SetActive(true);
            }
            else player.moveJoystick.gameObject.SetActive(false);
        });
        btnPlant.onClick.AddListener(() => OnClickCategory(btnPlant));
        btnNhanSo.onClick.AddListener(() => OnClickCategory(btnNhanSo));
        btnAnimal.onClick.AddListener(() => OnClickCategory(btnAnimal));
        btnThanhTuu.onClick.AddListener(OnThanhTuuClick);

        if (nextBtn != null)
        {
            nextBtn.onClick.RemoveAllListeners();
            nextBtn.onClick.AddListener(OnThanhTuuNextClick);
        }
        if (closeBtn != null)
        {
            closeBtn.onClick.AddListener(() =>
            {
                if (imgThanhTuu != null)
                    imgThanhTuu.gameObject.SetActive(false);
            });
        }

        // Setup navigation buttons
        if (btnNext != null)
        {
            btnNext.onClick.AddListener(ShowNextTooltip);
        }
        if (btnBack != null)
        {
            btnBack.onClick.AddListener(ShowPreviousTooltip);
        }

        if (detailBtn != null)
        {
            detailBtn.onClick.RemoveAllListeners();
            detailBtn.onClick.AddListener(OnDetailBtnClicked);
        }
        if (playVidBtn != null)
        {
            playVidBtn.onClick.RemoveAllListeners();
            playVidBtn.onClick.AddListener(OnPlayVidClicked);
        }
        if (btnCloseRawImage != null)
        {
            btnCloseRawImage.onClick.RemoveAllListeners();
            btnCloseRawImage.onClick.AddListener(OnCloseRawImageClicked);
        }
        viewFullBtn.onClick.AddListener(() =>
        {
            if (viewFullSprites == null || viewFullSprites.Count == 0) return;
            viewFull.gameObject.SetActive(true);
            viewFull.sprite = viewFullSprites[(int)_popupSource - 1];
        });
        closeViewBtn.onClick.AddListener(() =>
        {
            viewFull.gameObject.SetActive(false);
        });

        questBtn.onClick.AddListener(() =>
        {
            buttonPopup.gameObject.SetActive(false);
            imgBtn.gameObject.SetActive(false);
            player.isPlant = false;
            player.isNhanSo = false;
            player.isAnimal = false;
            player.moveJoystick.gameObject.SetActive(false);
            quizUIController.gameObject.SetActive(true);
            if (_popupSource == PopupSource.None) return;

            // Bạn đặt tên file CSV theo đúng enum _popupSource (VD: Plant, Animal, NhanSo)
            var path = _popupSource.ToString();
            string typeText;
            switch (_popupSource)
            {
                case PopupSource.Plant:
                    typeText = "Tế bào thực vật";
                    break;
                case PopupSource.Animal:
                    typeText = "Tế bào động vật";
                    break;
                case PopupSource.NhanSo:
                    typeText = "Tế bào nhân sơ";
                    break;
                default:
                    typeText = "Unknown";
                    break;
            }
            quizUIController.InitRandom20(path, typeText);
        });
    }

    void OnPlayVidClicked()
    {
        if (_popupSource == PopupSource.None)
        {
            Debug.LogWarning("Chưa chọn danh mục (thực vật / động vật / nhân số). Không có video tương ứng.");
            return;
        }

        if (videoPlayer == null)
        {
            Debug.LogWarning("Chưa gán VideoPlayer trên UIController.");
            return;
        }

        string fileName = $"{currentPathType}.mp4";
        string absolutePath = Path.Combine(Application.streamingAssetsPath, fileName);

#if !UNITY_ANDROID && !UNITY_WEBGL
        if (!File.Exists(absolutePath))
        {
            Debug.LogError($"Không tìm thấy video: {absolutePath}");
            return;
        }
#endif

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = BuildStreamingVideoUrl(absolutePath);
        if (videoPanel != null)
            videoPanel.SetActive(true);
        if (rawImage != null)
            rawImage.gameObject.SetActive(true);

        videoPlayer.Stop();
        videoPlayer.Play();
    }

    void OnCloseRawImageClicked()
    {
        if (videoPlayer != null)
            videoPlayer.Stop();

        if (rawImage != null)
            rawImage.gameObject.SetActive(false);

        if (videoPanel != null)
            videoPanel.SetActive(false);
    }

    /// <summary>
    /// VideoPlayer.url cần URL phù hợp từng nền tảng; StreamingAssets trên Android/WebGL khác PC.
    /// </summary>
    static string BuildStreamingVideoUrl(string absolutePath)
    {
        string normalized = absolutePath.Replace("\\", "/");

#if UNITY_ANDROID && !UNITY_EDITOR
        return normalized;
#elif UNITY_WEBGL && !UNITY_EDITOR
        return normalized;
#else
        if (normalized.Contains("://"))
            return normalized;
        return "file://" + normalized;
#endif
    }

    void OnDetailBtnClicked()
    {
        switch (_popupSource)
        {
            case PopupSource.Plant:
                player.isPlant = true;
                player.isNhanSo = false;
                player.isAnimal = false;
                break;
            case PopupSource.NhanSo:
                player.isNhanSo = true;
                player.isPlant = false;
                player.isAnimal = false;
                break;
            case PopupSource.Animal:
                player.isAnimal = true;
                player.isPlant = false;
                player.isNhanSo = false;
                break;
        }

        if (buttonPopup != null)
            buttonPopup.SetActive(false);
        if (imgThanhTuu != null)
            imgThanhTuu.gameObject.SetActive(false);
    }

    void OnThanhTuuClick()
    {
        btnPlant.image.color = inactiveColor;
        btnNhanSo.image.color = inactiveColor;
        btnAnimal.image.color = inactiveColor;
        btnThanhTuu.image.color = activeColor;

        if (imgThanhTuu != null)
        {
            imgThanhTuu.gameObject.SetActive(true);
            ApplyThanhTuuSpriteAtCurrentIndex();
        }
    }

    void OnThanhTuuNextClick()
    {
        if (thanhTuuSprites == null || thanhTuuSprites.Count == 0) return;
        _thanhTuuSpriteIndex = (_thanhTuuSpriteIndex + 1) % thanhTuuSprites.Count;
        ApplyThanhTuuSpriteAtCurrentIndex();
    }

    void ApplyThanhTuuSpriteAtCurrentIndex()
    {
        if (imgThanhTuu == null || thanhTuuSprites == null || thanhTuuSprites.Count == 0) return;
        _thanhTuuSpriteIndex = Mathf.Clamp(_thanhTuuSpriteIndex, 0, thanhTuuSprites.Count - 1);
        imgThanhTuu.sprite = thanhTuuSprites[_thanhTuuSpriteIndex];
    }

    void OnClickCategory(Button clickedBtn)
    {
        btnPlant.image.color = inactiveColor;
        btnNhanSo.image.color = inactiveColor;
        btnAnimal.image.color = inactiveColor;
        btnThanhTuu.image.color = inactiveColor;

        clickedBtn.image.color = activeColor;

        if (clickedBtn == btnPlant)
        {
            _popupSource = PopupSource.Plant;
            currentPathType = PathType.thuc_vat;
        }
        else if (clickedBtn == btnNhanSo)
        {
            _popupSource = PopupSource.NhanSo;
            currentPathType = PathType.nhan_so;
        }
        else if (clickedBtn == btnAnimal)
        {
            _popupSource = PopupSource.Animal;
            currentPathType = PathType.dong_vat;
        }
        else
            _popupSource = PopupSource.None;

        if (imgThanhTuu != null)
            imgThanhTuu.gameObject.SetActive(false);

        buttonPopup.SetActive(true);
    }
    // Method mới để bắt đầu navigation mode với tooltips
    public void StartTooltipNavigation(Transform parentTransform, List<ClickAble> curList)
    {
        if (parentTransform == null || tooltipUI == null) return;

        // Ẩn tooltip hiện tại nếu có
        if (tooltipUI != null)
        {
            tooltipUI.Hide();
        }

        // Thu thập tất cả clickable objects
        currentClickableList.Clear();
        currentClickableList = curList;

        if (currentClickableList.Count == 0)
        {
            Debug.LogWarning("Không tìm thấy clickable objects nào!");
            return;
        }

        // Bắt đầu navigation mode
        isNavigatingTooltips = true;
        currentTooltipIndex = 0;

        // Hiển thị tooltip đầu tiên
        ShowTooltipAtIndex(0);

        // Hiển thị navigation UI
        if (btnNext != null) btnNext.gameObject.SetActive(true);
        if (btnBack != null) btnBack.gameObject.SetActive(true);
        if (number != null) number.gameObject.SetActive(true);
        if (viewFullBtn != null) viewFullBtn.gameObject.SetActive(true);
    }

    // Hiển thị tooltip tại index cụ thể
    private void ShowTooltipAtIndex(int index)
    {
        if (currentClickableList == null || currentClickableList.Count == 0) return;
        if (index < 0 || index >= currentClickableList.Count) return;
        if (tooltipUI == null) return;

        // Dừng nhấp nháy object cũ
        if (currentBlinkingObject != null)
        {
            currentBlinkingObject.StopBlink();
            currentBlinkingObject = null;
        }

        // Lấy clickable object tại index
        ClickAble targetClickable = currentClickableList[index];
        if (targetClickable == null || !targetClickable.isValid) return;

        // Bắt đầu nhấp nháy object mới
        targetClickable.StartBlink();
        currentBlinkingObject = targetClickable;

        // Sử dụng tooltipUI có sẵn để hiển thị
        tooltipUI.Show(targetClickable, targetClickable);

        // Cập nhật số trang
        UpdatePageNumber();

    }

    // Hiển thị tooltip tiếp theo
    public void ShowNextTooltip()
    {
        if (!isNavigatingTooltips || currentClickableList == null || currentClickableList.Count == 0) return;

        currentTooltipIndex++;
        if (currentTooltipIndex >= currentClickableList.Count)
        {
            currentTooltipIndex = 0; // Quay lại đầu
        }

        ShowTooltipAtIndex(currentTooltipIndex);
    }

    // Hiển thị tooltip trước đó
    public void ShowPreviousTooltip()
    {
        if (!isNavigatingTooltips || currentClickableList == null || currentClickableList.Count == 0) return;

        currentTooltipIndex--;
        if (currentTooltipIndex < 0)
        {
            currentTooltipIndex = currentClickableList.Count - 1; // Quay lại cuối
        }

        ShowTooltipAtIndex(currentTooltipIndex);
    }

    // Cập nhật số trang hiển thị
    private void UpdatePageNumber()
    {
        if (number != null && currentClickableList != null && currentClickableList.Count > 0)
        {
            number.text = $"{currentTooltipIndex + 1}/{currentClickableList.Count}";
        }
    }


    // Dừng navigation mode
    public void StopTooltipNavigation()
    {
        // Dừng nhấp nháy object hiện tại
        if (currentBlinkingObject != null)
        {
            currentBlinkingObject.StopBlink();
            currentBlinkingObject = null;
        }

        isNavigatingTooltips = false;
        currentClickableList.Clear();
        currentTooltipIndex = 0;

        // Ẩn tooltip
        if (tooltipUI != null)
        {
            tooltipUI.Hide();
        }

        // Ẩn navigation UI
        if (btnNext != null) btnNext.gameObject.SetActive(false);
        if (btnBack != null) btnBack.gameObject.SetActive(false);
        if (number != null) number.gameObject.SetActive(false);
        if (viewFullBtn != null) viewFullBtn.gameObject.SetActive(false);
    }

    public void ShowTooltipForClickable(ClickAble clickable)
    {
        if (clickable == null || !clickable.isValid) return;

        if (tooltipUI != null)
        {
            tooltipUI.Show(clickable, clickable);
        }
    }

    // Click vào object hiện tại đang được hiển thị trong tooltip navigation
    public void ClickCurrentTooltipObject()
    {
        if (!isNavigatingTooltips || currentClickableList == null || currentClickableList.Count == 0) return;
        if (currentTooltipIndex < 0 || currentTooltipIndex >= currentClickableList.Count) return;

        ClickAble currentClickable = currentClickableList[currentTooltipIndex];
        if (currentClickable != null && currentClickable.isValid)
        {
            // Dừng nhấp nháy
            if (currentBlinkingObject != null)
            {
                currentBlinkingObject.StopBlink();
                currentBlinkingObject = null;
            }

            // Ẩn tooltip
            if (tooltipUI != null)
            {
                tooltipUI.Hide();
            }

            // Click vào object hiện tại
            currentClickable.OnClicked();
        }
    }

    // Kiểm tra xem có đang ở navigation mode không
    public bool IsNavigatingTooltips()
    {
        return isNavigatingTooltips;
    }

    // Lấy ClickAble object hiện tại đang được hiển thị
    public ClickAble GetCurrentTooltipClickable()
    {
        if (!isNavigatingTooltips || currentClickableList == null || currentClickableList.Count == 0) return null;
        if (currentTooltipIndex < 0 || currentTooltipIndex >= currentClickableList.Count) return null;
        
        return currentClickableList[currentTooltipIndex];
    }

    public void HideAll()
    {
        btnChoice.gameObject.SetActive(false);
        bottomPanel.gameObject.SetActive(false);
        btnMove.gameObject.SetActive(false);
    }
    public void ShowAll()
    {
        btnChoice.gameObject.SetActive(true);
        bottomPanel.gameObject.SetActive(true);
        btnMove.gameObject.SetActive(true);
    }
}

