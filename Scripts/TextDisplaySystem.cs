// GameObjectにアタッチして、URLを指定する。実際のダウンロード処理はwebinterface.csに記載。
// 取得したテキストは、TextMeshProのtextにセットする。

using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using TMPro;

public class TextDisplaySystem : UdonSharpBehaviour
{
    [Header("設定")]
    [SerializeField, Tooltip("取得するJSONのURL")]
    private VRCUrl jsonUrl;
    
    [SerializeField, Tooltip("テキスト表示用のTextMeshProコンポーネント")]
    private TextMeshProUGUI displayText;
    
    [SerializeField, Tooltip("ロード中表示用のGameObject")]
    private GameObject loadingIndicator;
    
    [SerializeField, Tooltip("エラー表示用のTextMeshProコンポーネント")]
    private TextMeshProUGUI errorText;
    
    [SerializeField, Tooltip("自動更新する間隔（秒）。0以下の場合は自動更新しない。")]
    private float autoRefreshInterval = 0f;
    
    // コンポーネントの参照
    private WebInterface webInterface;
    
    // 内部状態
    private float lastRefreshTime = 0f;
      void Start()
    {
        // WebInterfaceコンポーネントの取得
        webInterface = GetComponent<WebInterface>();
        if (webInterface == null)
        {
            // UdonSharpではAddComponentが使用できないため、事前にInspectorでアタッチする必要がある
            Debug.LogError("[TextDisplaySystem] WebInterfaceコンポーネントがGameObjectにアタッチされていません。同じGameObjectにWebInterfaceをアタッチしてください。");
        }
        
        // 初期設定
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }
        
        if (errorText != null)
        {
            errorText.gameObject.SetActive(false);
        }
        
        // URLの設定
        if (jsonUrl != null)
        {
            webInterface.SetUrl(jsonUrl);
        }
        
        // 初回データ取得
        RefreshData();
    }
    
    void Update()
    {
        // 自動更新が有効なら実行
        if (autoRefreshInterval > 0)
        {
            if (Time.time - lastRefreshTime >= autoRefreshInterval)
            {
                RefreshData();
                lastRefreshTime = Time.time;
            }
        }
    }
    
    public void RefreshData()
    {
        if (webInterface != null)
        {
            webInterface.LoadData();
        }
    }
    
    public void DisplayText(string text)
    {
        if (displayText != null)
        {
            displayText.text = text;
        }
        
        if (errorText != null)
        {
            errorText.gameObject.SetActive(false);
        }
    }
    
    public void DisplayError(string error)
    {
        if (errorText != null)
        {
            errorText.text = error;
            errorText.gameObject.SetActive(true);
        }
        
        // エラーログをUnityコンソールに記録
        LogError(error);
    }
    
    public void SetLoadingStatus(bool isLoading)
    {
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(isLoading);
        }
    }
    
    public void SetJsonUrl(VRCUrl newUrl)
    {
        jsonUrl = newUrl;
        if (webInterface != null)
        {
            webInterface.SetUrl(newUrl);
        }
    }
      private void LogError(string message)
    {
        Debug.LogError($"[TextDisplaySystem] {message}");
        // UdonSharpではファイル操作が使用できないため、エラーログの書き込み機能は削除
    }
}