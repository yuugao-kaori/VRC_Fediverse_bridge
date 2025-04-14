using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDK3.Data;

public class PostInfo : UdonSharpBehaviour
{
    [SerializeField]
    private TextDisplaySystem parentSystem;

    // UI要素
    [SerializeField] private TextMeshProUGUI usernameText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private TextMeshProUGUI timestampText;
    
    // データ
    public string instanceName;
    public string username;
    public string content;
    public string avatarUrl;
    public string fileUrls;
    public string timestamp;
    public string postId;

    public void Parse(DataDictionary dictionary)
    {
        // JSON構造に合わせてデータを解析
        instanceName = GetStringValue(dictionary, "instance_name");
        username = GetStringValue(dictionary, "username");
        content = GetStringValue(dictionary, "post_text");
        avatarUrl = GetStringValue(dictionary, "avatar_url");
        fileUrls = GetStringValue(dictionary, "file_urls");
        timestamp = GetStringValue(dictionary, "created_at");
        postId = GetStringValue(dictionary, "post_id");

        Debug.Log($"[Fediverse-Post] 投稿データを解析しました: {username}@{instanceName}, ID: {postId}");

        // UI要素を更新
        UpdateUI();
    }    public void UpdateUI()
    {
        // UI要素の自動検索
        FindUIComponents();
        
        bool hasValidUI = true;
        
        if (usernameText != null)
        {
            usernameText.text = username + "@" + instanceName;
            Debug.Log($"[Fediverse-Post] ユーザー名を設定: {username}@{instanceName}");
        }
        else
        {
            hasValidUI = false;
            Debug.LogWarning($"[Fediverse-Post] usernameTextがnullです (ID: {postId})");
        }

        if (contentText != null)
        {
            contentText.text = content;
            Debug.Log($"[Fediverse-Post] 投稿内容を設定: {(content.Length > 20 ? content.Substring(0, 20) + "..." : content)}");
        }
        else
        {
            hasValidUI = false;
            Debug.LogWarning($"[Fediverse-Post] contentTextがnullです (ID: {postId})");
        }

        if (timestampText != null)
        {
            // タイムスタンプを読みやすい形式に変換できればベスト
            // Udon制約により複雑な日付処理は難しいので、そのまま表示
            timestampText.text = timestamp;
            Debug.Log($"[Fediverse-Post] タイムスタンプを設定: {timestamp}");
        }
        else
        {
            hasValidUI = false;
            Debug.LogWarning($"[Fediverse-Post] timestampTextがnullです (ID: {postId})");
        }
        
        if (hasValidUI)
        {
            Debug.Log($"[Fediverse-Post] UI要素を更新しました: {username}@{instanceName}");
        }
        else
        {
            Debug.LogError($"[Fediverse-Post] UI要素が適切に設定されていません: {username}@{instanceName}");
        }
    }

    // UI要素を自動的に検索して設定
    private void FindUIComponents()
    {
        // UI要素が既に設定されている場合はスキップ
        if (usernameText != null && contentText != null && timestampText != null)
        {
            return;
        }

        Debug.Log("[Fediverse-Post] UI要素を自動検索します");
        
        // 子オブジェクト内のTextMeshProUGUIコンポーネントをすべて取得
        TextMeshProUGUI[] allTextComponents = GetComponentsInChildren<TextMeshProUGUI>(true);
        
        foreach (var textComponent in allTextComponents)
        {
            string objName = textComponent.gameObject.name.ToLower();
            
            if (usernameText == null && (objName.Contains("user") || objName.Contains("username") || objName.Contains("name")))
            {
                usernameText = textComponent;
                Debug.Log($"[Fediverse-Post] ユーザー名テキストを検出: {textComponent.gameObject.name}");
            }
            else if (contentText == null && (objName.Contains("content") || objName.Contains("post") || objName.Contains("text")))
            {
                contentText = textComponent;
                Debug.Log($"[Fediverse-Post] 投稿内容テキストを検出: {textComponent.gameObject.name}");
            }
            else if (timestampText == null && (objName.Contains("time") || objName.Contains("timestamp") || objName.Contains("date")))
            {
                timestampText = textComponent;
                Debug.Log($"[Fediverse-Post] タイムスタンプテキストを検出: {textComponent.gameObject.name}");
            }
        }
        
        // 自動検出の結果
        if (usernameText == null || contentText == null || timestampText == null)
        {
            Debug.LogWarning("[Fediverse-Post] 一部のUI要素を自動検出できませんでした");
        }
        else
        {
            Debug.Log("[Fediverse-Post] すべてのUI要素を自動検出しました");
        }
    }

    // JSONから安全に文字列値を取得するヘルパーメソッド
    private string GetStringValue(DataDictionary dict, string key)
    {
        if (dict.TryGetValue(key, out DataToken value) && value.TokenType == TokenType.String)
        {
            return value.String;
        }
        return "";
    }

    // 親システムへの参照を設定
    public void SetParentSystem(TextDisplaySystem system)
    {
        parentSystem = system;
    }

    // クリック時の処理
    public override void Interact()
    {
        if (parentSystem != null)
        {
            parentSystem.SelectPost(this);
        }
    }

    void Start()
    {
        Debug.Log("[Fediverse-Post] PostInfo.Start() called");
        
        // Start時にUI要素を確認し、不足しているものがあれば自動検出
        FindUIComponents();
    }
}
