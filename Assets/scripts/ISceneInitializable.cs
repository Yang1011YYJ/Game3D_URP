using System.Collections.Generic;

public interface ISceneInitializable
{
    // 回傳初始化步驟列表（每個場景可以不同）
    List<SceneInitStep> BuildInitSteps();
}
