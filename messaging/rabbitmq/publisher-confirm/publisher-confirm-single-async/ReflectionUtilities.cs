namespace PublisherConfirmAsync;

public static class ReflectionUtilities
{
    public static Type? GetFirstMatchingTypeFromCurrentDomainAssemblies(string typeName)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes().Where(x => x.FullName == typeName || x.Name == typeName))
            .FirstOrDefault();
    }
}
