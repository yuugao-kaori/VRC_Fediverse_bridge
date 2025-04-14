// VRCStringDownloaderを使用してウェブからデータを取得するクラス

using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

public class WebInterface : UdonSharpBehaviour
{
    [SerializeField]
    private VRCUrl url;
    
    [SerializeField]
    private TextDisplaySystem textDisplaySystem;
    
    private bool isLoading = false;
    
    void Start()
    {
        // textDisplaySystemが設定されていない場合は自動的に取得を試みる
        if (textDisplaySystem == null)
        {
            textDisplaySystem = GetComponent<TextDisplaySystem>();
            
            if (textDisplaySystem == null)
            {
                Debug.LogError("[WebInterface] TextDisplaySystemコンポーネントが見つかりません。同じGameObjectにTextDisplaySystemをアタッチするか、Inspectorで設定してください。");
            }
        }
    }
    
    public void LoadData()
    {
        if (isLoading) return;
        
        isLoading = true;
        
        // nullチェックを追加
        if (textDisplaySystem != null)
        {
            textDisplaySystem.SetLoadingStatus(true);
        }
        else
        {
            Debug.LogError("[WebInterface] TextDisplaySystem参照がnullです。データ取得は行いますが、表示の更新はできません。");
        }
        
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
    }
    
    public void SetUrl(VRCUrl newUrl)
    {
        url = newUrl;
    }
    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        isLoading = false;
        string resultText = result.Result;
        
        Debug.Log("[WebInterface] データ取得成功");
        
        // nullチェックを追加
        if (textDisplaySystem != null)
        {
            textDisplaySystem.DisplayText(resultText);
            textDisplaySystem.SetLoadingStatus(false);
        }
        else
        {
            Debug.LogError("[WebInterface] TextDisplaySystem参照がnullです。取得したデータを表示できません");
            // 大きなデータの場合はログに出力するとエディタがフリーズする可能性があるため、ログには出力しない
        }
    }
    
    public override void OnStringLoadError(IVRCStringDownload result)
    {
        isLoading = false;
        Debug.LogError($"データの取得に失敗しました: {result.ErrorCode} - {result.Error}");
        
        // nullチェックを追加
        if (textDisplaySystem != null)
        {
            textDisplaySystem.DisplayError($"エラー: {result.Error}");
            textDisplaySystem.SetLoadingStatus(false);
        }
        else
        {
            Debug.LogError("[WebInterface] TextDisplaySystem参照がnullです。エラーを表示できません。");
        }
    }
}