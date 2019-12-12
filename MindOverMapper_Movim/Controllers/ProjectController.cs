﻿using System;
using System.Linq;
using MindOverMapper_Movim.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MindOverMapper_Movim.Helpers;
using MindOverMapper_Movim.Services;
using System.Security.Claims;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MindOverMapper_Movim.Controllers
{
    [Route("api/[controller]")]
    public class ProjectController : Controller
    {

        private readonly MovimDbContext _context;
        private readonly ProjectService _service;
        private readonly AppSettings _appSettings;

        public ProjectController(MovimDbContext context, IOptions<AppSettings> appSettings)
        {
            _context = context;
            _appSettings = appSettings.Value;
            _service = new ProjectService();
        }

        private bool hasPermission(string userUid, string projUid)
        {
            var user = _context.User.Where(u => u.Uid == userUid).FirstOrDefault<User>();

            if (user == null)
            {
                return false;
            }
            else if (user.Type == "admin")
            {
                return true;
            }

            var proj = _context.Project.Where(p => p.Uid == projUid).FirstOrDefault<Project>();

            if (proj == null)
            {
                return false;
            }

            var per = _context.Permissions.Where(p => p.ProjId == proj.Id && p.UserId == user.Id).FirstOrDefault<Permissions>();
            return per != null;
        }

        [Authorize(Roles = "admin")]
        [HttpPost("{uid}/permissions")]
        public ActionResult AddUserToProject(string uid, [FromBody] ActionWithUsersRequest req)
        {
            foreach (string userUid in req.UserUids)
            {
                var project = _context.Project.Where(proj => proj.Uid == uid).FirstOrDefault<Project>();
                var user = _context.User.Where(u => u.Uid == userUid).FirstOrDefault<User>();

                if (user == null)
                {
                    return BadRequest(new { message = "Invalid User Uid. [" + userUid + "]" });
                }

                if (project == null)
                {
                    return BadRequest(new { message = "Invalid Project." });
                }

                var per = _context.Permissions.Where(p => p.UserId == user.Id && p.ProjId == project.Id).FirstOrDefault<Permissions>();

                if (per != null)
                {
                    return BadRequest(new { message = "User [" + userUid + "] Already has Permission." });
                }

                var newPer = new Permissions();
                newPer.UserId = user.Id;
                newPer.ProjId = project.Id;

                _context.Permissions.Add(newPer);
            }
            _context.SaveChanges();


            return Ok(new { message = "Success!" });

        }

        [Authorize(Roles = "admin")]
        [HttpGet("{uid}/permissions")]
        public ActionResult GetPermissions(string uid)
        {
            var results = _context.User.GroupJoin(_context.Permissions, u => u.Id, p => p.UserId, (user, per) => new { user, per })
                 .SelectMany(row => row.per.DefaultIfEmpty(), (x, y) => new { user = x.user, per = y })
                 .GroupJoin(_context.Project, userPer => userPer.per.ProjId, proj => proj.Id, (userPer, proj) => new { user = userPer.user, proj })
                 .SelectMany(row => row.proj.DefaultIfEmpty(), (x, y) => new { user = x.user, proj = y })
                 .Where(row => row.proj == null || row.proj.Uid == uid)
                 .Select(row => new {
                     Uid = row.user.Uid,
                     Email = row.user.Email,
                     FirstName = row.user.FirstName,
                     LastName = row.user.LastName,
                     Type = row.user.Type,
                     HasPermission = row.proj != null || row.user.Type == "admin"
                 }).ToList();
            return Ok(results);
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{uid}/permissions")]
        public ActionResult RemoveUserFromProject(string uid, [FromBody] ActionWithUsersRequest req)
        {
            foreach (string userUid in req.UserUids)
            {

                var project = _context.Project.Where(proj => proj.Uid == uid).FirstOrDefault<Project>();
                var user = _context.User.Where(u => u.Uid == userUid).FirstOrDefault<User>();

                if (user == null)
                {
                    return BadRequest(new { message = "Invalid User Uid. [" + userUid + "]" });
                }

                if (project == null)
                {
                    return BadRequest(new { message = "Invalid Project." });
                }

                var permission = _context.Permissions.Where(p => p.ProjId == project.Id && p.UserId == user.Id).FirstOrDefault<Permissions>();

                if (project == null)
                {
                    return BadRequest(new { message = "User [" + userUid + "] Doesn't Have Permission For This Project." });
                }

                _context.Permissions.Remove(permission);
            }
            _context.SaveChanges();
            return Ok(new { message = "Success!" });
        }

        [Authorize]
        [HttpGet]
        public ActionResult GetPreviews()
        {
            var uid = _service.GetUid(HttpContext.User.Identity as ClaimsIdentity);
            var type = _service.GetType(HttpContext.User.Identity as ClaimsIdentity);

            if (type == "admin")
            {
                var projects = _context.Project.Select(proj => new {
                    Uid = proj.Uid,
                    Title = proj.Title,
                    Description = proj.Description,
                    DateCreated = proj.DateCreated,
                }).ToList();

                return Ok(projects);
            }
            else
            {
                var projects = _context.User.Join(_context.Permissions, u => u.Id, p => p.UserId, (u, p) => new
                {
                    ProjectId = p.ProjId,
                    UserUid = u.Uid
                }).Join(_context.Project, per => per.ProjectId, proj => proj.Id, (per, proj) => new
                {
                    UserUid = per.UserUid,
                    Uid = proj.Uid,
                    Title = proj.Title,
                    Description = proj.Description,
                    DateCreated = proj.DateCreated,
                }).Where(per => per.UserUid == uid).Select(proj => new
                {
                    Uid = proj.Uid,
                    Title = proj.Title,
                    Description = proj.Description,
                    DateCreated = proj.DateCreated,
                }).ToList();

                return Ok(projects);
            }
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{uid}")]
        public ActionResult DeleteProject(string uid)
        {
            var proj = _context.Project.Where(p => p.Uid == uid).FirstOrDefault<Project>();

            if (proj == null)
            {
                return BadRequest(new { message = "Invalid project uid." });
            }

            var perm = _context.Permissions.Where(p => p.ProjId == proj.Id);
            var param = _context.ProjectParameters.Where(p => p.ProjectId == proj.Id).ToList<ProjectParameters>();

            foreach (ProjectParameters p in param) {
                if (p.LinkId != null)
                {
                    var link = _context.Links.Where(l => l.Id == p.LinkId).FirstOrDefault<Links>();
                    _context.Links.Remove(link);
                }
            }

            _context.ProjectParameters.RemoveRange(param);
            _context.Permissions.RemoveRange(perm);
            _context.Project.Remove(proj);
            _context.SaveChanges();
            return Ok(new { message = "Success!" });
        }

        [Authorize]
        [HttpPut("{uid}/state")]
        public ActionResult PutState(string uid, [FromBody] StateRequest req)
        {
            var userUid = _service.GetUid(HttpContext.User.Identity as ClaimsIdentity);

            if (!hasPermission(userUid, uid))
            {
                return Unauthorized(new { message = "User is not authorized" });
            }

            var proj = _context.Project.Where(p => p.Uid == uid).FirstOrDefault<Project>();

            if (proj == null)
            {
                return BadRequest(new { message = "Invalid Project." });
            }

            ProjectStimulus stim = JsonConvert.DeserializeObject<ProjectStimulus>(proj.Stimulus);

            if (stim.state.version != req.version)
            {
                HttpContext.Response.StatusCode = 403;
                return Json(stim.state);
            }

            stim.state = req;
            stim.state.version++;
            proj.Stimulus = JsonConvert.SerializeObject(stim);
            _context.SaveChanges();
            return Ok( stim.state );
        }

        [Authorize]
        [HttpGet("{uid}/state")]
        public ActionResult GetState(string uid)
        {
            var userUid = _service.GetUid(HttpContext.User.Identity as ClaimsIdentity);

            if (!hasPermission(userUid, uid))
            {
                return Unauthorized(new { message = "User is not authorized" });
            }

            var proj = _context.Project.Where(p => p.Uid == uid).FirstOrDefault<Project>();

            if (proj == null)
            {
                return BadRequest(new { message = "Invalid Project." });
            }

            ProjectStimulus stim = JsonConvert.DeserializeObject<ProjectStimulus>(proj.Stimulus);
            return Ok(stim.state);
        }

        [Authorize]
        [HttpGet("{uid}")]
        public ActionResult GetProjectInfo(string uid)
        {
            var userUid = _service.GetUid(HttpContext.User.Identity as ClaimsIdentity);

            if (!hasPermission(userUid, uid))
            {
                return BadRequest(new { message = "User doesn't have permission." });
            }

            var proj = _context.Project.Where(p => p.Uid == uid).FirstOrDefault<Project>();
            var param = _context.ProjectParameters.Where(p => p.ProjectId == proj.Id).ToList<ProjectParameters>();
            var exclusions = new List<Object>();
            var areasOfResearch = new List<Object>();

            foreach (ProjectParameters p in param)
            {
                Links link = null;
                if (p.LinkId != null)
                {
                    link = _context.Links.Where(l => l.Id == p.LinkId).FirstOrDefault<Links>();
                }
                if (p.Type == "e")
                {
                    Object e;
                    if (link == null)
                    {
                        e = new
                        {
                            content = p.Content
                        };
                    }
                    else
                    {
                        e = new
                        {
                            Content = p.Content,
                            Link = new
                            {
                                href = link.Href,
                                hrefName = link.Name
                            }
                        };
                    }
                    exclusions.Add(e);
                }
                else if (p.Type == "a")
                {
                    Object a;
                    if (link == null)
                    {
                        a = new
                        {
                            content = p.Content
                        };
                    }
                    else
                    {
                        a = new
                        {
                            Content = p.Content,
                            Link = new
                            {
                                href = link.Href,
                                hrefName = link.Name
                            }
                        };
                    }
                    areasOfResearch.Add(a);
                }
            }
            
            var res = new {
                uid = proj.Uid,
                title = proj.Title,
                description = proj.Description,
                exclusions = exclusions,
                definition = proj.Definition,
                areasOfResearch = areasOfResearch,
                dateCreated = proj.DateCreated
            };
            return Ok(res);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public ActionResult CreateProject([FromBody] CreateProjectRequest req)
        {
            var uid = _service.GetUid(HttpContext.User.Identity as ClaimsIdentity);
            var user = _context.User.Where(u => u.Uid == uid).FirstOrDefault<User>();

            Project proj = new Project();

            proj.Title = req.Title;
            proj.Uid = Guid.NewGuid().ToString();
            proj.Description = req.Description;
            proj.Definition = req.Definition;
            proj.DateCreated = DateTime.Now;
            proj.OwnerId = user.Id;

            var stimulus = new ProjectStimulus();
            stimulus.state = new StateRequest();
            stimulus.state.state = new MapStateRequest();
            stimulus.related = new List<StateItemRequest>();
            stimulus.unrelated = new List<StateItemRequest>();
            stimulus.state.state.items = new List<StateItemRequest>();
            stimulus.state.state.editorRootItemKey = "root";
            stimulus.state.state.rootItemKey = "root";
            stimulus.state.version = 0;

            var root = new StateItemRequest();
            root.content = req.ProblemStatement.Content;
            root.key = "root";
            root.subItemKeys = new List<string>();

            int i = 1;
            foreach (StimulusRequest stim in req.InitStimulus)
            {
                string key = "init" + i.ToString();
                var init = new StateItemRequest();
                root.subItemKeys.Add(key);
                init.key = key;
                init.parentKey = "root";
                init.subItemKeys = new List<string>();
                init.content = stim.Content;
                init.desc = stim.Description;
                if (stim.Link != null)
                {
                    init.desc += " " + stim.Link.HrefName + ": (" + stim.Link.Href + ")";
                }

                stimulus.state.state.items.Add(init);

                i++;
            }

            stimulus.state.state.items.Add(root);

            foreach (StimulusRequest stim in req.RelatedStimulus)
            {
                var related = new StateItemRequest();
                related.content = stim.Content;
                related.desc = stim.Description;
                if (stim.Link != null)
                {
                    related.desc += stim.Link.HrefName + ": (" + stim.Link.Href + ")";
                }
                stimulus.related.Add(related);
            }

            foreach (StimulusRequest stim in req.UnrelatedStimulus)
            {
                var unrelated = new StateItemRequest();
                unrelated.content = stim.Content;
                unrelated.desc = stim.Description;
                if (stim.Link != null)
                {
                    unrelated.desc += " " + stim.Link.HrefName + ": (" + stim.Link.Href + ")";
                } 
                stimulus.related.Add(unrelated);
            }

            proj.Stimulus = JsonConvert.SerializeObject(stimulus);

            _context.Project.Add(proj);
            _context.SaveChanges();

            
            List<ProjectParameters> parameters = new List<ProjectParameters>();

            foreach (ProjectParametersRequest param in req.Exclusions)
            {
                ProjectParameters p = new ProjectParameters();

                if (param.Link != null)
                {
                    Links l = new Links();
                    l.Href = param.Link.Href;
                    l.Name = param.Link.HrefName;
                    l.Uid = Guid.NewGuid().ToString();
                    _context.Links.Add(l);
                    _context.SaveChanges();
                    p.LinkId = l.Id;
                }
                p.Uid = Guid.NewGuid().ToString();
                p.Content = param.Content;
                p.Type = "e";
                p.ProjectId = proj.Id;
                parameters.Add(p);
            }

            foreach (ProjectParametersRequest param in req.AreasOfResearch)
            {
                ProjectParameters p = new ProjectParameters();

                if (param.Link != null)
                {
                    Links l = new Links();
                    l.Href = param.Link.Href;
                    l.Name = param.Link.HrefName;
                    l.Uid = Guid.NewGuid().ToString();
                    _context.Links.Add(l);
                    _context.SaveChanges();
                    p.LinkId = l.Id;
                }
                p.Uid = Guid.NewGuid().ToString();
                p.Content = param.Content;
                p.Type = "a";
                p.ProjectId = proj.Id;
                parameters.Add(p);
            }
            _context.ProjectParameters.AddRange(parameters);
            _context.SaveChanges();

            return Ok(new { message = "Success!" });
        }

    }
}
