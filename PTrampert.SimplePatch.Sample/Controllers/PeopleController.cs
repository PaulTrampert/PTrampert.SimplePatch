using Microsoft.AspNetCore.Mvc;

namespace PTrampert.SimplePatch.Sample.Controllers;

[ApiController]
[Route("[controller]")]
public class PeopleController : Controller
{
    private static readonly PeopleFakeDb _db = new();
    
    [HttpGet]
    public IEnumerable<PersonReadModel> GetPeople()
    {
        return _db.People;
    }

    [HttpGet("{id:int}")]
    public ActionResult<PersonReadModel> GetPerson(int id)
    {
        var person = _db.GetPersonById(id);
        if (person == null)
            return NotFound();
        return person;
    }

    [HttpPost]
    public ActionResult<PersonReadModel> AddPerson([FromBody] PersonWriteModel person)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        
        _db.AddPerson(person);
        var newPerson = _db.People.Last();
        return CreatedAtAction(nameof(GetPerson), new { id = newPerson.Id }, newPerson);
    }

    [HttpPut("{id:int}")]
    public ActionResult<PersonReadModel> UpdatePerson(int id, [FromBody] PersonWriteModel person)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var updatedPerson = _db.UpdatePerson(id, person);
        if (updatedPerson == null)
            return NotFound();
        return updatedPerson;
    }
    
    [HttpPatch("{id:int}")]
    public ActionResult<PersonReadModel> PatchPerson(
        int id,
        // PTrampert.SimplePatch automatically generates an implementation of IPatchObjectFor<PersonWriteModel>
        [FromBody] IPatchObject<PersonWriteModel> patchObject)
    {
        // Validation is preserved on the patch object, so we can check ModelState
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        
        var existingPerson = _db.GetPersonById(id);
        if (existingPerson == null)
            return NotFound();
        
        var patchedPerson = patchObject.Patch(existingPerson);
        var updatedPerson = _db.UpdatePerson(id, patchedPerson);
        
        return updatedPerson!;
    }

}