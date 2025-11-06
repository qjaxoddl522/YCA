using ChartAndGraph;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PieChartManager : MonoBehaviour
{
    [SerializeField] CanvasPieChart pieChart;
    [SerializeField] GameObject pieChartCategories;
    [SerializeField] GameObject pieChartCategoryPrefab;
    [SerializeField] Material baseMaterial;
    [SerializeField] TMP_Text periodText;
    
    struct CategoryInfo
    {
        public string name;
        public double amount;
        public Color color;
        public PieChartCategory categoryComponent;
    }
    
    Dictionary<string, CategoryInfo> categories = new Dictionary<string, CategoryInfo>();
    
    // 파스텔톤 색상을 위한 기준 Hue 값 (0~1 사이)
    private float baseHue = -1f;  // -1은 아직 초기화 안 됨을 의미
    
    [Header("파스텔톤 색상 설정")]
    [SerializeField]
    [Tooltip("각 카테고리마다 Hue 증가량 (0~1 사이, 권장: 0.09)")]
    private float hueIncrement = 0.09f;
    
    [SerializeField]
    [Tooltip("파스텔톤 채도 (0~1 사이, 낮을수록 파스텔, 권장: 0.4)")]
    [Range(0f, 1f)]
    private float saturation = 0.4f;
    
    [SerializeField]
    [Tooltip("파스텔톤 명도 (0~1 사이, 높을수록 밝음, 권장: 0.95)")]
    [Range(0f, 1f)]
    private float brightness = 0.95f;
    
    private int categoryCount = 0;  // 추가된 카테고리 개수

    void Start()
    {
        //if (pieChart != null)
        //{
        //    ClearAllCategories();
        //    AddCategory("Category A", 32);
        //    AddCategory("Category B", 21);
        //    AddCategory("Category C", 54);
        //}

        pieChart.StartAngle = 90f;
        pieChart.ClockWise = true;
    }

    public void AddCategory(string name, double amount, bool addUIToTop = false)
    {
        // 첫 번째 카테고리일 경우 랜덤하게 기준 Hue 선택
        if (baseHue < 0f)
        {
            baseHue = Random.Range(0f, 1f);
        }
        
        // 파스텔톤 색상 생성 (낮은 채도, 높은 명도)
        float currentHue = (baseHue + (categoryCount * hueIncrement)) % 1.0f;  // 1.0을 넘으면 다시 0으로
        Color pastelColor = Color.HSVToRGB(
            currentHue,      // Hue: 기준 색상에서 hueIncrement씩 증가
            saturation,      // Saturation: 낮은 채도로 파스텔톤
            brightness       // Value: 높은 명도로 밝은 톤
        );
        
        categoryCount++;
        
        // 같은 색상의 Material 동적 생성
        Material newMaterial = CreateMaterialWithColor(pastelColor);
        
        // UI 카테고리 생성 (텍스트)
        var categoryObj = Instantiate(pieChartCategoryPrefab, pieChartCategories.transform);
        var category = categoryObj.GetComponent<PieChartCategory>();
        
        // UI를 맨 위에 추가할지 여부
        if (addUIToTop)
        {
            categoryObj.transform.SetAsFirstSibling();
        }
        
        // 카테고리 정보 저장
        CategoryInfo info = new CategoryInfo
        {
            name = name,
            amount = amount,
            color = pastelColor,
            categoryComponent = category
        };
        categories[name] = info;
        
        // 임시로 0% 설정 (UpdateAllPercentages에서 재계산됨)
        category.SetCategory(name, pastelColor, 0f);

        // PieChart에 카테고리 추가 (같은 색상의 Material 사용)
        pieChart.DataSource.AddCategory(name, newMaterial);
        pieChart.DataSource.SetValue(name, amount);

        // 모든 카테고리의 퍼센트 재계산
        UpdateAllPercentages();

        // 호버 이벤트 리스너 추가
        pieChart.PieHovered.AddListener((PieChart.PieEventArgs label) =>
        {
            if (label.Category == name)
            {
                category.HighlightCategory();
            }
        });
        
        pieChart.NonHovered.AddListener(() =>
        {
            category.UnhighlightCategory();
        });
    }

    public void ClearAllCategories()
    {
        // PieChart의 모든 카테고리 제거
        pieChart.DataSource.Clear();
        
        // UI 카테고리 오브젝트 모두 제거
        foreach (Transform child in pieChartCategories.transform)
        {
            Destroy(child.gameObject);
        }
        
        // 내부 데이터 초기화
        categories.Clear();
        
        // 색상 초기화 (다음에 AddCategory 호출 시 새로운 랜덤 baseHue 선택)
        baseHue = -1f;
        categoryCount = 0;
    }

    /// <summary>
    /// 카테고리 값을 업데이트하고 모든 퍼센트 재계산
    /// </summary>
    public void UpdateCategoryValue(string name, double newAmount)
    {
        if (categories.ContainsKey(name))
        {
            // struct는 값 타입이므로 복사본을 수정하고 다시 할당해야 함
            var info = categories[name];
            info.amount = newAmount;
            categories[name] = info;
            
            pieChart.DataSource.SetValue(name, newAmount);
            UpdateAllPercentages();
        }
    }

    /// <summary>
    /// 모든 카테고리의 퍼센트를 재계산하여 업데이트
    /// </summary>
    void UpdateAllPercentages()
    {
        // 전체 합계 계산
        double totalAmount = 0;
        foreach (var info in categories.Values)
        {
            totalAmount += info.amount;
        }

        // 각 카테고리의 퍼센트 업데이트
        if (totalAmount > 0)
        {
            foreach (var info in categories.Values)
            {
                float percent = (float)((info.amount / totalAmount) * 100.0);
                
                // 카테고리의 현재 색상을 유지하면서 퍼센트만 업데이트
                info.categoryComponent.SetCategory(info.name, info.color, percent);
            }
        }
    }

    /// <summary>
    /// 지정된 색상으로 새로운 Material을 생성
    /// </summary>
    Material CreateMaterialWithColor(Color color)
    {
        Material newMat;
        
        if (baseMaterial != null)
        {
            // baseMaterial이 있으면 복사해서 사용
            newMat = new Material(baseMaterial);
        }
        else
        {
            // 없으면 Canvas Pie용 기본 Shader 사용
            Shader shader = Shader.Find("Chart/Canvas/Solid");
            if (shader == null)
            {
                Debug.LogWarning("Chart/Canvas/Solid shader not found! Using UI/Default instead.");
                shader = Shader.Find("UI/Default");
            }
            newMat = new Material(shader);
        }
        
        newMat.color = color;
        return newMat;
    }

    public void UpdatePeriod(string text)
    {
        if (periodText != null)
            periodText.text = text;
    }
}
