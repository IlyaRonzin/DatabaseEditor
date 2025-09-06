using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseEditor.Data;
using DatabaseEditor.Models;

namespace DatabaseEditor.Controllers
{
    public class TableController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TableController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Table/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var tables = await _context.GetUserTablesAsync();
                ViewBag.Tables = tables;
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View();
            }
        }

        // GET: /Table/Create
        public IActionResult Create()
        {
            return View(new TableModel());
        }

        // POST: /Table/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TableModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await CreateTableAsync(model);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(model);
            }
        }

        private async Task CreateTableAsync(TableModel model)
        {
            var sb = new StringBuilder();
            sb.Append($"CREATE TABLE \"{model.Name}\" (");

            foreach (var field in model.Fields)
            {
                var pgType = MapToPostgresType(field.Type);
                sb.Append($"\"{field.Name}\" {pgType}, ");
            }

            if (!string.IsNullOrEmpty(model.PrimaryKeyField))
            {
                sb.Append($"PRIMARY KEY (\"{model.PrimaryKeyField}\")");
            }
            else
            {
                sb.Length -= 2; 
            }

            sb.Append(");");

            await _context.Database.ExecuteSqlRawAsync(sb.ToString());
        }

        public async Task<IActionResult> Edit(string name)
        {
            try
            {
                var model = await GetTableSchemaAsync(name);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Table/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TableModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Упрощённое редактирование: drop + create (предупреждение о потере данных)
                await DeleteTableAsync(model.Name);
                await CreateTableAsync(model);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(model);
            }
        }

        private async Task<TableModel> GetTableSchemaAsync(string tableName)
        {
            var model = new TableModel { Name = tableName };
            var connection = _context.Database.GetDbConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT column_name, data_type, is_nullable, column_default
        FROM information_schema.columns
        WHERE table_name = @tableName AND table_schema = 'public';
    ";
            var param = new Npgsql.NpgsqlParameter
            {
                ParameterName = "@tableName",
                Value = tableName
            };
            command.Parameters.Add(param);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var fieldName = reader.GetString(0);
                var dataType = reader.GetString(1).ToLower();
                var fieldType = MapFromPostgresType(dataType);
                model.Fields.Add(new FieldModel { Name = fieldName, Type = fieldType });
            }

            await reader.CloseAsync();
            command.Parameters.Clear();
            command.CommandText = @"
        SELECT kcu.column_name
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
        WHERE tc.table_name = @tableName AND tc.constraint_type = 'PRIMARY KEY';
    ";
            var pkParam = new Npgsql.NpgsqlParameter
            {
                ParameterName = "@tableName",
                Value = tableName
            };
            command.Parameters.Add(pkParam);

            using var pkReader = await command.ExecuteReaderAsync();
            if (await pkReader.ReadAsync())
            {
                model.PrimaryKeyField = pkReader.GetString(0);
            }

            return model;
        }

        // POST: /Table/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string name)
        {
            try
            {
                await DeleteTableAsync(name);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task DeleteTableAsync(string name)
        {
            await _context.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS \"{name}\" CASCADE;");
        }

        private string MapToPostgresType(string type)
        {
            return type switch
            {
                "int" => "INTEGER",
                "double" => "DOUBLE PRECISION",
                "text" => "TEXT",
                "timestamp" => "TIMESTAMP",
                _ => throw new ArgumentException("Invalid field type")
            };
        }

        private string MapFromPostgresType(string pgType)
        {
            return pgType switch
            {
                "integer" => "int",
                "double precision" => "double",
                "text" => "text",
                "timestamp without time zone" => "timestamp",
                _ => "unknown"
            };
        }
    }
}