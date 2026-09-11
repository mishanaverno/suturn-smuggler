namespace Interior
{
    /// <summary>
    /// Орган управления, на который можно показать. Подпись обязательна: назначение органов
    /// игрок выясняет сам, но что именно случится по нажатию — прибор обязан сказать заранее.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Строка у курсора или прицела. Глагол, а не название узла.</summary>
        string Prompt { get; }
        bool Available { get; }
        void Interact();
    }
}
