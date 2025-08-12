namespace PTrampert.Optionals.Sample;

public class PeopleFakeDb
{
    public IEnumerable<PersonReadModel> People { get; private set; } =
    [
        new PersonReadModel
        {
            Id = 1,
            Name = "John Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Email = "john.doe@test.com",
            PhoneNumber = new PhoneNumber("123", "456-7890")
        },
        new PersonReadModel
        {
            Id = 2,
            Name = "Jane Smith",
            DateOfBirth = new DateTime(1985, 5, 15),
            Email = "jane.smith@test.com"
        }
    ];

    
    public PersonReadModel? GetPersonById(int id)
    {
        return People.FirstOrDefault(p => p.Id == id);
    }
    
    public void AddPerson(PersonWriteModel person)
    {
        var newId = People.Max(p => p.Id) + 1;
        var newPerson = new PersonReadModel
        {
            Id = newId,
            Name = person.Name,
            DateOfBirth = person.DateOfBirth,
            Email = person.Email,
            PhoneNumber = person.PhoneNumber
        };
        
        People = People.Append(newPerson);
    }
    
    public PersonReadModel? UpdatePerson(int id, PersonWriteModel person)
    {
        var existingPerson = GetPersonById(id);
        if (existingPerson == null) return null;

        People = People.Select(p => p.Id == id
            ? new PersonReadModel
            {
                Id = p.Id,
                Name = person.Name,
                DateOfBirth = person.DateOfBirth,
                Email = person.Email,
                PhoneNumber = person.PhoneNumber
            }
            : p);
        return GetPersonById(id);
    }
}