using UnityEngine;
using TMPro;

public class ObjectivesUI : MonoBehaviour
{
    [SerializeField] private ObjectiveSystem objectives;
    [SerializeField] private TextMeshProUGUI text;

    private void OnEnable()
    {
        objectives.OnObjectivesChanged += Refresh;
    }

    private void OnDisable()
    {
        objectives.OnObjectivesChanged -= Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    private void Refresh()
    {
        var goals = objectives.GetGoals();
        string result = "";

        for (int i = 0; i < goals.Length; i++)
        {
            result += $"{goals[i].type}: {objectives.GetProgress(i)} / {goals[i].amount}\n";
        }

        text.text = result;
    }
}