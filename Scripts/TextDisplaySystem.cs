// GameObjectにアタッチして、URLを指定する。実際のダウンロード処理はwebinterface.csに記載。
// Fediverse投稿を取得してGameObjectとして表示する

using System;
using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using TMPro;
using VRC.SDK3.Data;
using UnityEngine.UI;

public class TextDisplaySystem : UdonSharpBehaviour
{
    [Header("設定")]
    [SerializeField, Tooltip("取得するJSONのURL")]
    private VRCUrl jsonUrl;
    
    [SerializeField, Tooltip("投稿のプレハブ（PostInfoコンポーネントがアタッチされていること）")]
    private GameObject postPrefab;
    
    [SerializeField, Tooltip("投稿を表示するコンテナ（ScrollViewのContentなど）")]
    private Transform postsContainer;
    
    [SerializeField, Tooltip("ロード中表示用のGameObject")]
    private GameObject loadingIndicator;
    
    [SerializeField, Tooltip("エラー表示用のTextMeshProコンポーネント")]
    private TextMeshProUGUI errorText;
    
    [SerializeField, Tooltip("投稿がない場合に表示するGameObject")]
    private GameObject noPostsMessage;
    
    [SerializeField, Tooltip("自動更新する間隔（秒）。0以下の場合は自動更新しない。")]
    private float autoRefreshInterval = 0f;
    
    [SerializeField, Tooltip("表示する投稿の最大数")]
    private int maxPostsToDisplay = 10;
    
    [Header("選択された投稿の詳細表示")]
    [SerializeField, Tooltip("選択された投稿の詳細を表示するパネル")]
    private GameObject detailPanel;
    
    [SerializeField, Tooltip("詳細パネルのユーザー名テキスト")]
    private TextMeshProUGUI detailUsername;
    
    [SerializeField, Tooltip("詳細パネルの投稿内容テキスト")]
    private TextMeshProUGUI detailPostText;
    
    [SerializeField, Tooltip("詳細パネルのタイムスタンプテキスト")]
    private TextMeshProUGUI detailTimestamp;
    
    // コンポーネントの参照
    private WebInterface webInterface;
    
    // 内部状態
    private float lastRefreshTime = 0f;
    private GameObject[] loadedPosts;
    private PostInfo selectedPost;    void Start()
    {
        // WebInterfaceコンポーネントの取得
        webInterface = GetComponent<WebInterface>();
        if (webInterface == null)
        {
            // UdonSharpではAddComponentが使用できないため、事前にInspectorでアタッチする必要がある
            Debug.LogError("[Fediverse] WebInterfaceコンポーネントがGameObjectにアタッチされていません。同じGameObjectにWebInterfaceをアタッチしてください。");
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
        
        if (noPostsMessage != null)
        {
            noPostsMessage.SetActive(false);
        }
        
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }
        
        // postsContainerの設定チェック
        if (postsContainer != null)
        {
            // VerticalLayoutGroupコンポーネントがあるか確認
            VerticalLayoutGroup verticalLayout = postsContainer.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout == null)
            {
                Debug.LogError("[Fediverse] postsContainerにVerticalLayoutGroupがありません。投稿が重なって表示される可能性があります。Unity EditorでVerticalLayoutGroupコンポーネントを追加してください。");
            }
            else
            {
                Debug.Log("[Fediverse] postsContainerにVerticalLayoutGroupが設定されています。spacing: " + verticalLayout.spacing);
            }
            
            // ContentSizeFitterコンポーネントがあるか確認
            ContentSizeFitter contentFitter = postsContainer.GetComponent<ContentSizeFitter>();
            if (contentFitter == null)
            {
                Debug.LogWarning("[Fediverse] postsContainerにContentSizeFitterがありません。スクロール表示に問題が生じる可能性があります。Unity EditorでContentSizeFitterコンポーネントを追加してください。");
            }
            else
            {
                Debug.Log("[Fediverse] postsContainerにContentSizeFitterが設定されています。verticalFit: " + contentFitter.verticalFit);
                
                // ContentSizeFitterの設定確認
                if (contentFitter.verticalFit != ContentSizeFitter.FitMode.PreferredSize)
                {
                    Debug.LogWarning("[Fediverse] ContentSizeFitterのverticalFitがPreferredSizeに設定されていません。スクロール表示が正常に動作しない可能性があります。");
                }
            }
            
            // RectTransformの設定チェック
            RectTransform containerRect = postsContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                Debug.Log("[Fediverse] Contentコンテナのサイズ: 幅=" + containerRect.rect.width + ", 高さ=" + containerRect.rect.height);
            }
        }
        else
        {
            Debug.LogError("[Fediverse] postsContainer（投稿を表示するコンテナ）が設定されていません。");
        }
        
        // ポストプレハブのチェック
        if (postPrefab != null)
        {
            // PostInfoコンポーネントがあるか確認
            PostInfo prefabPostInfo = postPrefab.GetComponent<PostInfo>();
            if (prefabPostInfo == null)
            {
                Debug.LogError("[Fediverse] 投稿プレハブにPostInfoコンポーネントがありません。");
                prefabPostInfo = postPrefab.GetComponentInChildren<PostInfo>(true);
                if (prefabPostInfo != null)
                {
                    Debug.Log("[Fediverse] 投稿プレハブの子オブジェクトからPostInfoを見つけました。");
                }
            }
            
            // プレハブのRectTransformをチェック
            RectTransform prefabRect = postPrefab.GetComponent<RectTransform>();
            if (prefabRect != null)
            {
                Debug.Log("[Fediverse] 投稿プレハブのサイズ: 幅=" + prefabRect.rect.width + ", 高さ=" + prefabRect.rect.height);
                
                // 高さが0の場合は警告
                if (prefabRect.rect.height <= 0)
                {
                    Debug.LogWarning("[Fediverse] 投稿プレハブの高さが0以下です。レイアウトに問題が生じる可能性があります。");
                }
            }
        }
        else
        {
            Debug.LogError("[Fediverse] 投稿プレハブが設定されていません。");
        }
        
        // URLの設定
        if (jsonUrl != null && webInterface != null)
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
            // 更新前に選択状態をクリア
            if (selectedPost != null)
            {
                selectedPost = null;
                if (detailPanel != null)
                {
                    detailPanel.SetActive(false);
                }
            }
            
            webInterface.LoadData();
        }
    }
    
    // 特定の投稿を選択状態にする
    public void SelectPost(PostInfo post)
    {
        selectedPost = post;
        
        if (detailPanel != null && post != null)
        {
            detailPanel.SetActive(true);
            
            if (detailUsername != null)
            {
                detailUsername.text = post.username + "@" + post.instanceName;
            }
            
            if (detailPostText != null)
            {
                detailPostText.text = post.content;
            }
            
            if (detailTimestamp != null)
            {
                detailTimestamp.text = post.timestamp;
            }
        }
    }
    
    // 詳細表示を閉じる
    public void CloseDetails()
    {
        if (detailPanel != null)
        {
            detailPanel.SetActive(false);
        }
        selectedPost = null;
    }
      // 現在ロードされている投稿をクリア
    private void ClearLoadedPosts()
    {
        if (loadedPosts != null)
        {
            for (int i = 0; i < loadedPosts.Length; i++)
            {
                if (loadedPosts[i] != null)
                {
                    // TaAGatheringListSystemと同様に、Destroyを使用する
                    Destroy(loadedPosts[i]);
                }
            }
        }
        loadedPosts = null;
    }public void DisplayText(string jsonText)
    {
        // エラーテキストを非表示にする
        if (errorText != null)
        {
            errorText.gameObject.SetActive(false);
        }
        
        // JSONパース処理
        if (VRCJson.TryDeserializeFromJson(jsonText, out DataToken result))
        {
            Debug.Log("[Fediverse] JSON解析成功");
            
            if (result.TokenType != TokenType.DataList)
            {
                DisplayError("受信したJSONが配列形式ではありません");
                return;
            }
            
            var postsArray = result.DataList;
            int postsCount = postsArray.Count;
            
            Debug.Log($"[Fediverse] 取得した投稿数: {postsCount}");
            
            // 投稿がない場合
            if (postsCount == 0)
            {
                ClearLoadedPosts();
                if (noPostsMessage != null)
                {
                    noPostsMessage.SetActive(true);
                }
                return;
            }
            
            if (noPostsMessage != null)
            {
                noPostsMessage.SetActive(false);
            }
            
            // 表示する投稿数を決定（最大数以内）
            int displayCount = Mathf.Min(postsCount, maxPostsToDisplay);
            Debug.Log($"[Fediverse] 表示する投稿数: {displayCount}");
            
            // 既存の投稿を削除
            ClearLoadedPosts();
            
            // 新しい投稿をインスタンス化して表示
            loadedPosts = new GameObject[displayCount];
            
            // postsContainerのnullチェック
            if (postsContainer == null)
            {
                Debug.LogError("[Fediverse] postsContainerがnullです。投稿を表示できません。");
                return;
            }
            else
            {
                Debug.Log($"[Fediverse] postsContainer: {postsContainer.name}, 子オブジェクト数: {postsContainer.childCount}");
            }
            
            // 投稿プレハブが非アクティブであることを確認
            if (postPrefab != null)
            {
                bool wasActive = postPrefab.activeSelf;
                postPrefab.SetActive(false);
                  // PostInfoコンポーネントの存在をより詳細に確認
                var prefabPostInfo = postPrefab.GetComponent<PostInfo>();
                Debug.Log($"[Fediverse] 投稿プレハブ: {postPrefab.name}, PostInfoコンポーネント: {(prefabPostInfo != null ? "あり" : "なし")}");
                if (prefabPostInfo == null)
                {
                    Debug.LogError("[Fediverse] 投稿プレハブにPostInfoコンポーネントがありません。実行時に追加します。");
                }
                
                for (int i = 0; i < displayCount; i++)
                {
                    if (postsContainer != null)
                    {                        // TaAGatheringListSystemと同様の方法でインスタンス化
                        var postGameObject = Instantiate(postPrefab);
                        if (postGameObject == null)
                        {
                            Debug.LogError($"[Fediverse] 投稿{i}のインスタンス化に失敗しました");
                            continue;
                        }
                        
                        Debug.Log($"[Fediverse] 投稿{i}をインスタンス化しました: {postGameObject.name}");
                        postGameObject.transform.SetParent(postsContainer, false);
                          // RectTransformの確認と位置設定
                        RectTransform rectTransform = postGameObject.GetComponent<RectTransform>();
                        if (rectTransform != null)
                        {
                            // ワールド空間位置は親のLayoutGroupに任せるのでリセット
                            rectTransform.localPosition = Vector3.zero;
                            // 横幅を親の幅に合わせる
                            rectTransform.anchorMin = new Vector2(0, 0);
                            rectTransform.anchorMax = new Vector2(1, 0);
                            rectTransform.pivot = new Vector2(0.5f, 0);
                            // サイズはContentSizeFitterに任せるため、高さはデフォルト値を維持
                            rectTransform.sizeDelta = new Vector2(0, rectTransform.sizeDelta.y);
                        }
                                  // 既存のPostInfoを確認し、なければ追加
                        var postInfo = postGameObject.GetComponent<PostInfo>();
                        if (postInfo == null)
                        {
                            // 子オブジェクトでも探してみる
                            postInfo = postGameObject.GetComponentInChildren<PostInfo>(true);                            if (postInfo == null)
                            {
                                Debug.LogError($"[Fediverse] 投稿{i}にPostInfoコンポーネントがありません。実行時には追加できません。");
                                // UdonSharpではAddComponentにtypeofを使った型指定はできないため、この方法は使用不可
                                // 代わりにPostInfoがアタッチされていないことを通知するだけにする
                            }
                            else
                            {
                                Debug.Log($"[Fediverse] 投稿{i}の子オブジェクトからPostInfoコンポーネントを見つけました");
                            }
                        }
                        
                        if (postInfo != null)
                        {
                            Debug.Log($"[Fediverse] 投稿{i}のPostInfoコンポーネントを取得しました");
                            // 親への参照を設定
                            postInfo.SetParentSystem(this);
                            
                            // JSONデータをパース
                            postInfo.Parse(postsArray[i].DataDictionary);
                            
                            // 投稿のGameObject名をポストIDにする（あれば）
                            if (!string.IsNullOrEmpty(postInfo.postId))
                            {
                                postGameObject.name = "Post_" + postInfo.postId;
                            }
                            else
                            {
                                postGameObject.name = "Post_" + i;
                            }
                              // JSONデータパース後に強制的に再度UpdateUIを呼び出す
                            // これによりUI要素が確実に更新される
                            postInfo.UpdateUI();
                            
                            // ポストを表示
                            postGameObject.SetActive(true);
                            Debug.Log($"[Fediverse] 投稿{i}をアクティブに設定しました: {postGameObject.name}");
                            
                            // 生成した投稿を配列に保存
                            loadedPosts[i] = postGameObject;
                        }
                        else
                        {
                            Debug.LogError("[Fediverse] プレハブにPostInfoコンポーネントが見つかりません");
                        }
                    }
                }
                
                // オリジナルのアクティブ状態を復元
                postPrefab.SetActive(wasActive);
                  // 投稿生成結果の確認
                if (loadedPosts != null)
                {
                    int successCount = 0;
                    for (int i = 0; i < loadedPosts.Length; i++)
                    {
                        if (loadedPosts[i] != null)
                        {
                            // PostInfoコンポーネントの存在を再確認
                            var postInfo = loadedPosts[i].GetComponent<PostInfo>();
                            if (postInfo != null) 
                            {
                                successCount++;
                                Debug.Log($"[Fediverse] 投稿{i}のPostInfoコンポーネントが有効です");
                            }
                            else
                            {
                                Debug.LogError($"[Fediverse] 投稿{i}のPostInfoコンポーネントが見つかりません");
                            }
                        }
                    }
                    Debug.Log($"[Fediverse] 投稿生成結果: {successCount}/{loadedPosts.Length}個の投稿が正常に生成されました");
                    Debug.Log($"[Fediverse] postsContainer内の子オブジェクト数: {postsContainer.childCount}");                // レイアウトコンポーネントの確認
                    VerticalLayoutGroup verticalLayout = postsContainer.GetComponent<VerticalLayoutGroup>();
                    ContentSizeFitter contentFitter = postsContainer.GetComponent<ContentSizeFitter>();
                    
                    if (verticalLayout == null)
                    {
                        Debug.LogWarning("[Fediverse] postsContainerにVerticalLayoutGroupが設定されていません。投稿が重なって表示される可能性があります。");
                        // 注意: UdonSharpではAddComponentが使用できないため、
                        // この警告を見た場合は手動でUnityエディタ上でVerticalLayoutGroupを追加する必要があります
                        // ここではコードで追加できないため警告のみとします
                    }
                    else
                    {
                        // VerticalLayoutGroupの設定をログに出力
                        Debug.Log("[Fediverse] VerticalLayoutGroupが設定されています。spacing: " + verticalLayout.spacing);
                        
                        // 設定の確認と調整（パラメータだけを変更することはUdonSharpでも可能）
                        if (verticalLayout.spacing < 5f)
                        {
                            // 間隔が狭すぎる場合は調整（推奨値は10〜20）
                            Debug.Log("[Fediverse] VerticalLayoutGroupのspacingが小さいため、投稿が重なって見える可能性があります。Unity Editor上で調整することをお勧めします。");
                        }
                          // パディングの確認（UdonSharpでの制限を回避するために直接アクセスしない）
                        // paddingプロパティは直接アクセスできないため、単に一般的な注意を記録
                        Debug.Log("[Fediverse] VerticalLayoutGroupのパディングが適切に設定されていることを確認してください。上下の余白を考慮することをお勧めします。");
                    }
                    
                    // ContentSizeFitterの確認
                    if (contentFitter == null)
                    {
                        Debug.LogWarning("[Fediverse] postsContainerにContentSizeFitterが設定されていません。スクロール表示に問題が生じる可能性があります。");
                    }
                    else
                    {
                        // 設定の確認
                        Debug.Log("[Fediverse] ContentSizeFitterが設定されています。verticalFit: " + contentFitter.verticalFit);
                    }
                    
                    // レイアウト更新を強制
                    RectTransform rectTransform = postsContainer.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        // レイアウトの再計算を強制
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                        Debug.Log("[Fediverse] レイアウトを更新しました");
                    }
                }
            }
            else
            {
                Debug.LogError("[Fediverse] 投稿プレハブが設定されていません");
            }
            
            // 最初の投稿を選択（あれば）
            if (loadedPosts != null && loadedPosts.Length > 0 && loadedPosts[0] != null)
            {
                PostInfo firstPost = loadedPosts[0].GetComponent<PostInfo>();
                if (firstPost != null)
                {
                    SelectPost(firstPost);
                }
            }
        }
        else
        {
            Debug.LogError("[Fediverse] JSONパースエラー");
            DisplayError("JSONの解析に失敗しました");
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