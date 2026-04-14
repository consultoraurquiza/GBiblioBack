# 📚 Sistema de Gestión de Biblioteca Institucional

Un sistema moderno, rápido y escalable para la gestión integral de bibliotecas escolares e institucionales. Desarrollado aplicando principios de **Arquitectura Limpia (Clean Architecture)** en el backend y una interfaz reactiva orientada a la experiencia de usuario (UX) en el frontend.

Este proyecto fue diseñado desde cero para modernizar el flujo de trabajo bibliotecario, solucionando problemas reales como la importación de catálogos antiguos (Koha), la automatización de metadatos mediante Web Scraping y la gestión de inventario físico en tiempo real.

## ✨ Características y Módulos Principales

* **📦 Motor de Migración Koha (MARC21/XML):** Importación masiva de registros bibliográficos y ejemplares desde exportaciones de sistemas legados. Incluye un motor de filtrado dinámico (por nombre de institución) para depurar catálogos compartidos o provinciales en el momento de la ingesta.
* **🤖 Ecosistema de Scrapers y APIs Externas:** * **Tiendas Comerciales:** Web Scraper propio diseñado para navegar y extraer portadas y descripciones en español desde tiendas locales (ej. Yenny/El Ateneo).
  * **Archivos Históricos:** Integración con la API REST de **Open Library** y **Google Books** para el rescate de portadas de ediciones descatalogadas mediante ISBN o Título/Autor.
  * **Modo Autónomo:** Soporte *fallback* para subir portadas de forma local en caso de cortes de internet o apuntes propios de la institución.
* **🗂️ Catalogación Profesional Avanzada:** Soporte nativo para clasificación CDU/Dewey, generación automática de signaturas topográficas (Cutter-Sanborn), control de autoridades y sistema de etiquetas predictivo con tesauro local.
* **🔄 Mostrador de Préstamos Ágil (Optimistic UI):** Pantalla de gestión de préstamos y devoluciones con actualizaciones instantáneas de estado en memoria (sin recargas de página). Control riguroso de inventario físico y disponibilidad por copias exactas.
* **🔐 Seguridad y Autenticación:** Sistema de sesiones seguras basado en JSON Web Tokens (JWT) y cookies `HttpOnly` para proteger rutas administrativas y endpoints críticos.

## 🛠️ Stack Tecnológico y Arquitectura

### 💻 Frontend (Cliente Web)
* **Framework:** Next.js (React) con TypeScript.
* **Estilos:** Tailwind CSS con sistema de *Theming* dinámico (Lumina, Obsidian, etc.) para reducir la fatiga visual del bibliotecario.
* **Manejo de Estado y Sesión:** `js-cookie` para lectura de tokens y sincronización de sesiones cliente-servidor.

### ⚙️ Backend (API RESTful)
* **Plataforma:** .NET (C#).
* **Patrones de Diseño:** Clean Architecture (API, Application, Domain, Infrastructure), Inyección de Dependencias, Patrón Repositorio.
* **Base de Datos:** PostgreSQL a través de Entity Framework Core.

### 📦 Paquetes y Librerías Clave
* **`HtmlAgilityPack`**: Motor principal utilizado por el servicio de Web Scraping para parsear el DOM, ejecutar consultas XPath y extraer datos crudos de tiendas externas.
* **`System.Xml` / Lector MARC**: Utilizado en el proceso de ETL (Extract, Transform, Load) para desglosar y mapear las etiquetas complejas de los archivos exportados por Koha.
* **`Microsoft.AspNetCore.Authentication.JwtBearer`**: Emisión y validación de tokens JWT para la protección de la API.
* **`System.Text.Json`**: Para la serialización de alto rendimiento y el consumo de APIs externas públicas.

## 🚀 Guía de Instalación Local

### Requisitos Previos
* Node.js (v18+) y npm.
* .NET SDK (v7.0 o superior).
* Instancia de PostgreSQL (local o Docker).

### 1. Configuración de la Base de Datos
Crear una base de datos en PostgreSQL (ej. `GestionBibliotecaDB`) y actualizar la cadena de conexión en el archivo `appsettings.json` o `appsettings.Development.json` dentro del proyecto Backend.

### 2. Levantar el Backend (C#)
```bash
cd Backend/API
dotnet restore
dotnet ef database update  # Aplica las migraciones estructurales
dotnet run
La API quedará escuchando en http://localhost:5078.

3. Levantar el Frontend (Next.js)
Bash
cd Frontend
npm install
npm run dev
El cliente web y el OPAC estarán disponibles en http://localhost:3000.

📂 Estructura del Repositorio
/Backend - Contiene la solución de C# dividida en proyectos lógicos (Domain, Application, Infrastructure, API). Contiene la lógica dura de los controladores MARC y el Scraper.

/Frontend - Aplicación Next.js modularizada. Contiene las vistas del catálogo, la lógica de paginación por URL, la gestión de modales de importación y el enrutamiento.

Diseñado y desarrollado para modernizar la gestión documental y facilitar el acceso a la lectura en el ámbito educativo.
