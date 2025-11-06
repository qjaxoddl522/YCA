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
        // 랜덤 색상 생성
        Color randomColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
        
        // 같은 색상의 Material 동적 생성
        Material newMaterial = CreateMaterialWithColor(randomColor);
        
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
            color = randomColor,
            categoryComponent = category
        };
        categories[name] = info;
        
        // 임시로 0% 설정 (UpdateAllPercentages에서 재계산됨)
        category.SetCategory(name, randomColor, 0f);

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
