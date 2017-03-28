﻿' ################################################################################
' #                             EMBER MEDIA MANAGER                              #
' ################################################################################
' ################################################################################
' # This file is part of Ember Media Manager.                                    #
' #                                                                              #
' # Ember Media Manager is free software: you can redistribute it and/or modify  #
' # it under the terms of the GNU General Public License as published by         #
' # the Free Software Foundation, either version 3 of the License, or            #
' # (at your option) any later version.                                          #
' #                                                                              #
' # Ember Media Manager is distributed in the hope that it will be useful,       #
' # but WITHOUT ANY WARRANTY; without even the implied warranty of               #
' # MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the                #
' # GNU General Public License for more details.                                 #
' #                                                                              #
' # You should have received a copy of the GNU General Public License            #
' # along with Ember Media Manager.  If not, see <http://www.gnu.org/licenses/>. #
' ################################################################################

Imports System.Windows.Forms
Imports System.IO
Imports System.Xml.Serialization
Imports System.Data.SQLite
Imports NLog
Imports System.Text.RegularExpressions

''' <summary>
''' Class defining and implementing the interface to the database
''' </summary>
''' <remarks></remarks>
Public Class Database

#Region "Fields"

    Shared logger As Logger = LogManager.GetCurrentClassLogger()

    Friend WithEvents bwPatchDB As New System.ComponentModel.BackgroundWorker

    ReadOnly _connStringTemplate As String = "Data Source=""{0}"";Version=3;Compress=True"
    Protected _myvideosDBConn As SQLiteConnection
    ' NOTE: This will use another DB because: can grow alot, Don't want to stress Media DB with this stuff
    'Protected _jobsDBConn As SQLiteConnection

#End Region 'Fields

#Region "Events"

    Public Event GenericEvent(ByVal mType As Enums.AddonEventType, ByRef _params As List(Of Object))

#End Region 'Events

#Region "Properties"

    Public ReadOnly Property MyVideosDBConn() As SQLiteConnection
        Get
            Return _myvideosDBConn
        End Get
    End Property

    'Public ReadOnly Property JobsDBConn() As SQLiteConnection
    '    Get
    '        Return _jobsDBConn
    '    End Get
    'End Property

#End Region

#Region "Methods"
    ''' <summary>
    ''' add or update actor
    ''' </summary>
    ''' <param name="strActor">actor name</param>
    ''' <param name="strThumbURL">thumb URL</param>
    ''' <param name="strThumb">local thumb path</param>
    ''' <param name="strIMDB">IMDB ID of actor</param>
    ''' <param name="intTMDB">TMDB ID of actor</param>
    ''' <param name="bIsActor"><c>True</c> if adding an actor, <c>False</c> if adding a Creator, Director, Writer or something else without ID's and images to refresh if already exist in actors table</param>
    ''' <returns><c>ID</c> of actor in actors table</returns>
    ''' <remarks></remarks>
    Private Function AddActor(ByVal strActor As String, ByVal strThumbURL As String, ByVal strThumb As String, ByVal strIMDB As String, ByVal intTMDB As Integer, ByVal bIsActor As Boolean) As Long
        Dim bAlreadyExist As Boolean = False
        Dim lngID As Long = -1

        Using SQLcommand_select_actors As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand_select_actors.CommandText = String.Format("SELECT idActor FROM actors WHERE strActor LIKE ?", strActor)
            Dim par_select_actors_strActor As SQLiteParameter = SQLcommand_select_actors.Parameters.Add("par_select_actors_strActor", DbType.String, 0, "strActor")
            par_select_actors_strActor.Value = strActor
            Using SQLreader As SQLiteDataReader = SQLcommand_select_actors.ExecuteReader()
                While SQLreader.Read
                    bAlreadyExist = True
                    lngID = CInt(SQLreader("idActor"))
                    Exit While
                End While
            End Using
        End Using

        If Not bAlreadyExist Then
            Using SQLcommand_insert_actors As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand_insert_actors.CommandText = "INSERT INTO actors (idActor, strActor, strThumb, strIMDB, strTMDB) VALUES (NULL,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM actors;"
                Dim par_insert_actors_strActor As SQLiteParameter = SQLcommand_insert_actors.Parameters.Add("par_actors_strActor", DbType.String, 0, "strActor")
                Dim par_insert_actors_strThumb As SQLiteParameter = SQLcommand_insert_actors.Parameters.Add("par_actors_strThumb", DbType.String, 0, "strThumb")
                Dim par_insert_actors_strIMDB As SQLiteParameter = SQLcommand_insert_actors.Parameters.Add("par_actors_strIMDB", DbType.String, 0, "strIMDB")
                Dim par_insert_actors_strTMDB As SQLiteParameter = SQLcommand_insert_actors.Parameters.Add("par_actors_strTMDB", DbType.String, 0, "strTMDB")
                par_insert_actors_strActor.Value = strActor
                par_insert_actors_strThumb.Value = strThumbURL
                par_insert_actors_strIMDB.Value = strIMDB
                par_insert_actors_strTMDB.Value = intTMDB
                lngID = CInt(SQLcommand_insert_actors.ExecuteScalar())
            End Using
        ElseIf bIsActor Then
            Using SQLcommand_update_actors As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand_update_actors.CommandText = String.Format("UPDATE actors SET strThumb=?, strIMDB=?, strTMDB=? WHERE idActor={0}", lngID)
                Dim par_update_actors_strThumb As SQLiteParameter = SQLcommand_update_actors.Parameters.Add("par_actors_strThumb", DbType.String, 0, "strThumb")
                Dim par_update_actors_strIMDB As SQLiteParameter = SQLcommand_update_actors.Parameters.Add("par_actors_strIMDB", DbType.String, 0, "strIMDB")
                Dim par_update_actors_strTMDB As SQLiteParameter = SQLcommand_update_actors.Parameters.Add("par_actors_strTMDB", DbType.String, 0, "strTMDB")
                par_update_actors_strThumb.Value = strThumbURL
                par_update_actors_strIMDB.Value = strIMDB
                par_update_actors_strTMDB.Value = intTMDB
                SQLcommand_update_actors.ExecuteNonQuery()
            End Using
        End If

        If Not lngID = -1 Then
            If Not String.IsNullOrEmpty(strThumb) Then
                SetArtForItem(lngID, "actor", "thumb", strThumb)
            End If
        End If

        Return lngID
    End Function

    Private Sub AddArtistToMusicVideo(ByVal idMVideo As Long, ByVal idArtist As Long)
        AddToLinkTable("artistlinkmusicvideo", "idArtist", idArtist, "idMVideo", idMVideo)
    End Sub

    Private Sub AddCast(ByVal lngMediaID As Long, ByVal strTable As String, ByVal strField As String, ByVal lstCast As List(Of MediaContainers.Person))
        If lstCast Is Nothing Then Return

        Dim iOrder As Integer = 0
        For Each actor As MediaContainers.Person In lstCast
            Dim idActor = AddActor(actor.Name, actor.URLOriginal, actor.LocalFilePath, actor.IMDB, actor.TMDB, True)
            AddLinkToActor(strTable, idActor, strField, lngMediaID, actor.Role, iOrder)
            iOrder += 1
        Next
    End Sub

    Private Sub AddCreatorToTvShow(ByVal lngShowID As Long, ByVal lngActorID As Long)
        AddToLinkTable("creatorlinktvshow", "idActor", lngActorID, "idShow", lngShowID)
    End Sub

    Private Function AddCountry(ByVal strCountry As String) As Long
        If String.IsNullOrEmpty(strCountry) Then Return -1
        Return AddToTable("country", "idCountry", "strCountry", strCountry)
    End Function

    Private Sub AddCountryToMovie(ByVal lngMovieID As Long, ByVal lngCountryID As Long)
        AddToLinkTable("countrylinkmovie", "idCountry", lngCountryID, "idMovie", lngMovieID)
    End Sub

    Private Sub AddDirectorToEpisode(ByVal lngEpisodeID As Long, ByVal lngDirectorID As Long)
        AddToLinkTable("directorlinkepisode", "idDirector", lngDirectorID, "idEpisode", lngEpisodeID)
    End Sub

    Private Sub AddCountryToTVShow(ByVal lngShowID As Long, ByVal lngCountryID As Long)
        AddToLinkTable("countrylinktvshow", "idCountry", lngCountryID, "idShow", lngShowID)
    End Sub

    Private Sub AddDirectorToMovie(ByVal lngMovieID As Long, ByVal lngDirectorID As Long)
        AddToLinkTable("directorlinkmovie", "idDirector", lngDirectorID, "idMovie", lngMovieID)
    End Sub

    Private Sub AddDirectorToMusicVideo(ByVal lngMVideoID As Long, ByVal lngDirectorID As Long)
        AddToLinkTable("directorlinkmusicvideo", "idDirector", lngDirectorID, "idMVideo", lngMVideoID)
    End Sub

    Private Sub AddDirectorToTvShow(ByVal lngShowID As Long, ByVal lngDirectorID As Long)
        AddToLinkTable("directorlinktvshow", "idDirector", lngDirectorID, "idShow", lngShowID)
    End Sub

    Private Function AddGenre(ByVal strGenre As String) As Long
        If String.IsNullOrEmpty(strGenre) Then Return -1
        Dim ID As Long = AddToTable("genre", "idGenre", "strGenre", strGenre)
        LoadAllGenres()
        Return ID
    End Function

    Private Sub AddGenreToMovie(ByVal lngMovieID As Long, ByVal lngGenreID As Long)
        AddToLinkTable("genrelinkmovie", "idGenre", lngGenreID, "idMovie", lngMovieID)
    End Sub

    Private Sub AddGenreToMusicVideo(ByVal lngMVideoID As Long, ByVal lngGenreID As Long)
        AddToLinkTable("genrelinkmusicvideo", "idGenre", lngGenreID, "idMVideo", lngMVideoID)
    End Sub

    Private Sub AddGenreToTvShow(ByVal lngShowID As Long, ByVal lngGenreID As Long)
        AddToLinkTable("genrelinktvshow", "idGenre", lngGenreID, "idShow", lngShowID)
    End Sub

    Private Sub AddGuestStar(ByVal lngMediaID As Long, ByVal strTable As String, ByVal strField As String, ByVal lstCast As List(Of MediaContainers.Person))
        If lstCast Is Nothing Then Return

        Dim iOrder As Integer = 0
        For Each actor As MediaContainers.Person In lstCast
            Dim idActor = AddActor(actor.Name, actor.URLOriginal, actor.LocalFilePath, actor.IMDB, actor.TMDB, True)
            AddLinkToGuestStar(strTable, idActor, strField, lngMediaID, actor.Role, iOrder)
            iOrder += 1
        Next
    End Sub
    ''' <summary>
    ''' add an actor to an actorlink* table
    ''' </summary>
    ''' <param name="strTable">link table name without "actorlink" prefix("episode", "movie" or "tvshow")</param>
    ''' <param name="lngActorID">ID of actor in table actors</param>
    ''' <param name="strField">field name in <c>table</c> without "id" prefix("Episode", "Movie" or "Show")</param>
    ''' <param name="lngSecondID">ID of <c>field</c> </param>
    ''' <param name="strRole">actors role</param>
    ''' <param name="lngOrder">actors order</param>
    ''' <returns><c>True</c> if the actor link has been created, <c>False</c> otherwise</returns>
    ''' <remarks></remarks>
    Private Function AddLinkToActor(ByVal strTable As String, ByVal lngActorID As Long, ByVal strField As String,
                                    ByVal lngSecondID As Long, ByVal strRole As String,
                                    ByVal lngOrder As Long) As Boolean
        Dim doesExist As Boolean = False

        Using SQLcommand_select_actorlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand_select_actorlink.CommandText = String.Format("SELECT * FROM actorlink{0} WHERE idActor={1} AND id{2}={3};", strTable, lngActorID, strField, lngSecondID)
            Using SQLreader As SQLiteDataReader = SQLcommand_select_actorlink.ExecuteReader()
                While SQLreader.Read
                    doesExist = True
                    Exit While
                End While
            End Using
        End Using

        If Not doesExist Then
            Using SQLcommand_insert_actorlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand_insert_actorlink.CommandText = String.Format("INSERT INTO actorlink{0} (idActor, id{1}, strRole, iOrder) VALUES ({2},{3},?,{4})", strTable, strField, lngActorID, lngSecondID, lngOrder)
                Dim par_insert_actors_strRole As SQLiteParameter = SQLcommand_insert_actorlink.Parameters.Add("par_insert_actors_strRole", DbType.String, 0, "strRole")
                par_insert_actors_strRole.Value = strRole
                SQLcommand_insert_actorlink.ExecuteNonQuery()
            End Using
            Return True
        Else
            Return False
        End If
    End Function
    ''' <summary>
    ''' add an actor to an actorlink* table
    ''' </summary>
    ''' <param name="strTable">link table name without "actorlink" prefix("episode", "movie" or "tvshow")</param>
    ''' <param name="lngActorID">ID of actor in table actors</param>
    ''' <param name="strField">field name in <c>table</c> without "id" prefix("Episode", "Movie" or "Show")</param>
    ''' <param name="lngSecondID">ID of <c>field</c> </param>
    ''' <param name="strRole">actors role</param>
    ''' <param name="lngOrder">actors order</param>
    ''' <returns><c>True</c> if the actor link has been created, <c>False</c> otherwise</returns>
    ''' <remarks></remarks>
    Private Function AddLinkToGuestStar(ByVal strTable As String, ByVal lngActorID As Long, ByVal strField As String,
                                    ByVal lngSecondID As Long, ByVal strRole As String,
                                    ByVal lngOrder As Long) As Boolean
        Dim doesExist As Boolean = False

        Using SQLcommand_select_gueststarlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand_select_gueststarlink.CommandText = String.Format("SELECT * FROM gueststarlink{0} WHERE idActor={1} AND id{2}={3};", strTable, lngActorID, strField, lngSecondID)
            Using SQLreader As SQLiteDataReader = SQLcommand_select_gueststarlink.ExecuteReader()
                While SQLreader.Read
                    doesExist = True
                    Exit While
                End While
            End Using
        End Using

        If Not doesExist Then
            Using SQLcommand_insert_gueststarlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand_insert_gueststarlink.CommandText = String.Format("INSERT INTO gueststarlink{0} (idActor, id{1}, strRole, iOrder) VALUES ({2},{3},?,{4})", strTable, strField, lngActorID, lngSecondID, lngOrder)
                Dim par_insert_gueststar_strRole As SQLiteParameter = SQLcommand_insert_gueststarlink.Parameters.Add("par_insert_gueststar_strRole", DbType.String, 0, "strRole")
                par_insert_gueststar_strRole.Value = strRole
                SQLcommand_insert_gueststarlink.ExecuteNonQuery()
            End Using
            Return True
        Else
            Return False
        End If
    End Function

    Private Function AddSet(ByVal strSet As String) As Long
        If String.IsNullOrEmpty(strSet) Then Return -1
        Return AddToTable("sets", "idSet", "strSet", strSet)
    End Function

    Private Sub AddTVShowToMovie(ByVal lngMovieID As Long, ByVal strTVShow As String)
        Dim idShow As Long = -1
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT idShow FROM tvshow WHERE Title LIKE '{0}';", strTVShow)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    idShow = CLng(SQLreader("idShow"))
                    Exit While
                End While
            End Using
        End Using
        If Not idShow = -1 Then
            AddToLinkTable("movielinktvshow", "idMovie", lngMovieID, "idShow", idShow)
        End If
    End Sub

    Private Function AddStudio(ByVal strStudio As String) As Long
        If String.IsNullOrEmpty(strStudio) Then Return -1
        Return AddToTable("studio", "idStudio", "strStudio", strStudio)
    End Function

    Private Sub AddStudioToMovie(ByVal lngMovieID As Long, ByVal lngStudioID As Long)
        AddToLinkTable("studiolinkmovie", "idStudio", lngStudioID, "idMovie", lngMovieID)
    End Sub

    Private Sub AddStudioToTvShow(ByVal lngShowID As Long, ByVal lngStudioID As Long)
        AddToLinkTable("studiolinktvshow", "idStudio", lngStudioID, "idShow", lngShowID)
    End Sub

    Private Function AddTag(ByVal strTag As String) As Long
        If String.IsNullOrEmpty(strTag) Then Return -1
        Return AddToTable("tag", "idTag", "strTag", strTag)
    End Function

    Private Function AddToLinkTable(ByVal strTable As String, ByVal strFirstField As String, ByVal lngFirstID As Long, ByVal strSecondField As String, ByVal lngSecondID As Long,
                               Optional ByVal strTypeField As String = "", Optional ByVal strType As String = "") As Boolean
        Dim doesExist As Boolean = False

        Using SQLcommand_select As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand_select.CommandText = String.Format("SELECT * FROM {0} WHERE {1}={2} AND {3}={4}", strTable, strFirstField, lngFirstID, strSecondField, lngSecondID)
            If Not String.IsNullOrEmpty(strTypeField) AndAlso Not String.IsNullOrEmpty(strType) Then
                SQLcommand_select.CommandText = String.Concat(SQLcommand_select.CommandText, String.Format(" AND {0}='{1}'", strTypeField, strType))
            End If
            Using SQLreader As SQLiteDataReader = SQLcommand_select.ExecuteReader()
                While SQLreader.Read
                    doesExist = True
                    Exit While
                End While
            End Using
        End Using

        If Not doesExist Then
            Using SQLcommand_insert As SQLiteCommand = _myvideosDBConn.CreateCommand()
                If String.IsNullOrEmpty(strTypeField) AndAlso String.IsNullOrEmpty(strType) Then
                    SQLcommand_insert.CommandText = String.Format("INSERT INTO {0} ({1},{2}) VALUES ({3},{4})", strTable, strFirstField, strSecondField, lngFirstID, lngSecondID)
                Else
                    SQLcommand_insert.CommandText = String.Format("INSERT INTO {0} ({1},{2},{3}) VALUES ({4},{5},'{6}')", strTable, strFirstField, strSecondField, strTypeField, lngFirstID, lngSecondID, strType)
                End If
                SQLcommand_insert.ExecuteNonQuery()
                Return True
            End Using
        Else
            Return False
        End If
    End Function

    Private Function AddToTable(ByVal strTable As String, ByVal strFirstField As String, ByVal strSecondField As String, ByVal strValue As String) As Long
        Dim doesExist As Boolean = False
        Dim ID As Long = -1

        Using SQLcommand_select As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand_select.CommandText = String.Format("SELECT {0} FROM {1} WHERE {2} LIKE ?", strFirstField, strTable, strSecondField)
            Dim par_select_secondField As SQLiteParameter = SQLcommand_select.Parameters.Add("par_select_secondField", DbType.String, 0, strSecondField)
            par_select_secondField.Value = strValue
            Using SQLreader As SQLiteDataReader = SQLcommand_select.ExecuteReader()
                While SQLreader.Read
                    doesExist = True
                    ID = CInt(SQLreader(strFirstField))
                    Exit While
                End While
            End Using
        End Using

        If Not doesExist Then
            Using SQLcommand_insert As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand_insert.CommandText = String.Format("INSERT INTO {0} ({1}, {2}) VALUES (NULL, ?); SELECT LAST_INSERT_ROWID() FROM {0};", strTable, strFirstField, strSecondField)
                Dim par_insert_secondField As SQLiteParameter = SQLcommand_insert.Parameters.Add("par_insert_secondField", DbType.String, 0, strSecondField)
                par_insert_secondField.Value = strValue
                ID = CInt(SQLcommand_insert.ExecuteScalar())
            End Using
        End If

        Return ID
    End Function

    Private Sub AddTagToItem(ByVal lngMediaID As Long, ByVal lngTagID As Long, ByVal strType As String)
        If String.IsNullOrEmpty(strType) Then Return
        AddToLinkTable("taglinks", "idTag", lngTagID, "idMedia", lngMediaID, "media_type", strType)
    End Sub

    Private Sub AddWriterToEpisode(ByVal lngEpisodeID As Long, ByVal lngWriterID As Long)
        AddToLinkTable("writerlinkepisode", "idWriter", lngWriterID, "idEpisode", lngEpisodeID)
    End Sub

    Private Sub AddWriterToMovie(ByVal lngMovieID As Long, ByVal lngWriterID As Long)
        AddToLinkTable("writerlinkmovie", "idWriter", lngWriterID, "idMovie", lngMovieID)
    End Sub

    Private Sub SetArtForItem(ByVal lngMediaId As Long, ByVal strMediaType As String, ByVal strArtType As String, ByVal strUrl As String)
        Dim doesExist As Boolean = False
        Dim ID As Long = -1
        Dim oldURL As String = String.Empty

        Using SQLcommand_select_art As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand_select_art.CommandText = String.Format("SELECT art_id, url FROM art WHERE media_id={0} AND media_type='{1}' AND type='{2}'", lngMediaId, strMediaType, strArtType)
            Using SQLreader As SQLiteDataReader = SQLcommand_select_art.ExecuteReader()
                While SQLreader.Read
                    doesExist = True
                    ID = CInt(SQLreader("art_id"))
                    oldURL = SQLreader("url").ToString
                    Exit While
                End While
            End Using
        End Using

        If Not doesExist Then
            Using SQLcommand_insert_art As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand_insert_art.CommandText = String.Format("INSERT INTO art(media_id, media_type, type, url) VALUES ({0}, '{1}', '{2}', ?)", lngMediaId, strMediaType, strArtType)
                Dim par_insert_art_url As SQLiteParameter = SQLcommand_insert_art.Parameters.Add("par_insert_art_url", DbType.String, 0, "url")
                par_insert_art_url.Value = strUrl
                SQLcommand_insert_art.ExecuteNonQuery()
            End Using
        Else
            If Not strUrl = oldURL Then
                Using SQLcommand_update_art As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_update_art.CommandText = String.Format("UPDATE art SET url=(?) WHERE art_id={0}", ID)
                    Dim par_update_art_url As SQLiteParameter = SQLcommand_update_art.Parameters.Add("par_update_art_url", DbType.String, 0, "url")
                    par_update_art_url.Value = strUrl
                    SQLcommand_update_art.ExecuteNonQuery()
                End Using
            End If
        End If
    End Sub

    Public Function GetArtForItem(ByVal lngMediaId As Long, ByVal strMediaType As String, ByVal strArtType As String) As String
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT url FROM art WHERE media_id={0} AND media_type='{1}' AND type='{2}'", lngMediaId, strMediaType, strArtType)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Return SQLreader("url").ToString
                    Exit While
                End While
            End Using
        End Using
        Return String.Empty
    End Function

    ''' <summary>
    ''' Iterates db entries to check if the paths to the movie or TV files are valid. 
    ''' If not, remove all entries pertaining to the movie.
    ''' </summary>
    ''' <param name="bCleanMovies">If <c>True</c>, process the movie files</param>
    ''' <param name="bCleanTVShows">If <c>True</c>, process the TV files</param>
    ''' <param name="lngSourceID">Optional. If provided, only process entries from that source.</param>
    ''' <remarks></remarks>
    Public Sub Clean(ByVal bCleanMovies As Boolean, ByVal bCleanMovieSets As Boolean, ByVal bCleanTVShows As Boolean, Optional ByVal lngSourceID As Long = -1)
        Dim fInfo As FileInfo
        Dim tPath As String = String.Empty
        Dim sPath As String = String.Empty

        logger.Info("Cleaning videodatabase started")

        Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
            If bCleanMovies Then
                logger.Info("Cleaning movies started")
                Dim MoviePaths As List(Of String) = GetAll_MoviePaths()
                MoviePaths.Sort()

                'get a listing of sources and their recursive properties
                Dim SourceList As New List(Of DBSource)
                Dim tSource As DBSource

                Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    If lngSourceID = -1 Then
                        SQLcommand.CommandText = "SELECT * FROM moviesource;"
                    Else
                        SQLcommand.CommandText = String.Format("SELECT * FROM moviesource WHERE idSource = {0}", lngSourceID)
                    End If
                    Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                        While SQLreader.Read
                            SourceList.Add(Load_Source_Movie(Convert.ToInt64(SQLreader("idSource"))))
                        End While
                    End Using
                End Using

                Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    If lngSourceID = -1 Then
                        SQLcommand.CommandText = "SELECT MoviePath, idMovie, idSource, Type FROM movie ORDER BY MoviePath DESC;"
                    Else
                        SQLcommand.CommandText = String.Format("SELECT MoviePath, idMovie, idSource, Type FROM movie WHERE idSource = {0} ORDER BY MoviePath DESC;", lngSourceID)
                    End If
                    Using SQLReader As SQLiteDataReader = SQLcommand.ExecuteReader()
                        While SQLReader.Read
                            If Not File.Exists(SQLReader("MoviePath").ToString) OrElse
                                Not Master.eSettings.Options.Filesystem.ValidVideoExts.Contains(Path.GetExtension(SQLReader("MoviePath").ToString).ToLower) OrElse
                                Master.ExcludedDirs.Exists(Function(s) SQLReader("MoviePath").ToString.ToLower.StartsWith(s.ToLower)) Then
                                MoviePaths.Remove(SQLReader("MoviePath").ToString)
                                Master.DB.Delete_Movie(Convert.ToInt64(SQLReader("idMovie")), True)
                            ElseIf Master.eSettings.MovieSkipLessThan > 0 Then
                                fInfo = New FileInfo(SQLReader("MoviePath").ToString)
                                If ((Not Master.eSettings.MovieSkipStackedSizeCheck OrElse
                                    Not FileUtils.Common.isStacked(fInfo.FullName)) AndAlso
                                    fInfo.Length < Master.eSettings.MovieSkipLessThan * 1048576) Then
                                    MoviePaths.Remove(SQLReader("MoviePath").ToString)
                                    Master.DB.Delete_Movie(Convert.ToInt64(SQLReader("idMovie")), True)
                                End If
                            Else
                                tSource = SourceList.OrderByDescending(Function(s) s.Path).FirstOrDefault(Function(s) s.ID = Convert.ToInt64(SQLReader("idSource")))
                                If tSource IsNot Nothing AndAlso FileUtils.Common.CheckOnlineStatus(tSource, True) Then
                                    If Directory.GetParent(Directory.GetParent(SQLReader("MoviePath").ToString).FullName).Name.ToLower = "bdmv" Then
                                        tPath = Directory.GetParent(Directory.GetParent(SQLReader("MoviePath").ToString).FullName).FullName
                                    Else
                                        tPath = Directory.GetParent(SQLReader("MoviePath").ToString).FullName
                                    End If
                                    sPath = FileUtils.Common.GetDirectory(tPath).ToLower
                                    If Not tSource.Recursive AndAlso tPath.Length > tSource.Path.Length AndAlso If(sPath = "video_ts" OrElse sPath = "bdmv", tPath.Substring(tSource.Path.Length).Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar).Count > 2, tPath.Substring(tSource.Path.Length).Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar).Count > 1) Then
                                        MoviePaths.Remove(SQLReader("MoviePath").ToString)
                                        Master.DB.Delete_Movie(Convert.ToInt64(SQLReader("idMovie")), True)
                                    ElseIf Not Convert.ToBoolean(SQLReader("Type")) AndAlso tSource.IsSingle AndAlso Not MoviePaths.Where(Function(s) SQLReader("MoviePath").ToString.ToLower.StartsWith(tSource.Path.ToLower)).Count = 1 Then
                                        MoviePaths.Remove(SQLReader("MoviePath").ToString)
                                        Master.DB.Delete_Movie(Convert.ToInt64(SQLReader("idMovie")), True)
                                    End If
                                Else
                                    'orphaned
                                    MoviePaths.Remove(SQLReader("MoviePath").ToString)
                                    Master.DB.Delete_Movie(Convert.ToInt64(SQLReader("idMovie")), True)
                                End If
                            End If
                        End While
                    End Using
                End Using
                logger.Info("Cleaning movies done")
            End If

            If bCleanMovieSets Then
                logger.Info("Cleaning moviesets started")
                Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand.CommandText = "SELECT sets.idSet, COUNT(setlinkmovie.idMovie) AS 'Count' FROM sets LEFT OUTER JOIN setlinkmovie ON sets.idSet = setlinkmovie.idSet GROUP BY sets.idSet ORDER BY sets.idSet COLLATE NOCASE;"
                    Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                        While SQLreader.Read
                            If Convert.ToInt64(SQLreader("Count")) = 0 Then
                                Master.DB.Delete_MovieSet(Convert.ToInt64(SQLreader("idSet")), True)
                            End If
                        End While
                    End Using
                End Using
                logger.Info("Cleaning moviesets done")
            End If

            If bCleanTVShows Then
                logger.Info("Cleaning tv shows started")
                Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    If lngSourceID = -1 Then
                        SQLcommand.CommandText = "SELECT files.strFilename, episode.idEpisode FROM files INNER JOIN episode ON (files.idFile = episode.idFile) ORDER BY files.strFilename;"
                    Else
                        SQLcommand.CommandText = String.Format("SELECT files.strFilename, episode.idEpisode FROM files INNER JOIN episode ON (files.idFile = episode.idFile) WHERE episode.idSource = {0} ORDER BY files.strFilename;", lngSourceID)
                    End If

                    Using SQLReader As SQLiteDataReader = SQLcommand.ExecuteReader()
                        While SQLReader.Read
                            If Not File.Exists(SQLReader("strFilename").ToString) OrElse Not Master.eSettings.Options.Filesystem.ValidVideoExts.Contains(Path.GetExtension(SQLReader("strFilename").ToString).ToLower) OrElse
                                Master.ExcludedDirs.Exists(Function(s) SQLReader("strFilename").ToString.ToLower.StartsWith(s.ToLower)) Then
                                Master.DB.Delete_TVEpisode(Convert.ToInt64(SQLReader("idEpisode")), True, False)
                            End If
                        End While
                    End Using

                    logger.Info("Removing tvshows with no more existing local episodes")
                    SQLcommand.CommandText = "DELETE FROM tvshow WHERE NOT EXISTS (SELECT episode.idShow FROM episode WHERE episode.idShow = tvshow.idShow AND NOT episode.idFile = -1);"
                    SQLcommand.ExecuteNonQuery()
                    logger.Info("Removing seasons with no more existing tvshows")
                    SQLcommand.CommandText = "DELETE FROM seasons WHERE idShow NOT IN (SELECT idShow FROM tvshow);"
                    SQLcommand.ExecuteNonQuery()
                    logger.Info("Removing episodes with no more existing tvshows")
                    SQLcommand.CommandText = "DELETE FROM episode WHERE idShow NOT IN (SELECT idShow FROM tvshow);"
                    SQLcommand.ExecuteNonQuery()
                    logger.Info("Removing episodes with orphaned paths")
                    SQLcommand.CommandText = "DELETE FROM episode WHERE NOT EXISTS (SELECT files.idFile FROM files WHERE files.idFile = episode.idFile OR episode.idFile = -1)"
                    SQLcommand.ExecuteNonQuery()
                    logger.Info("Removing orphaned paths")
                    SQLcommand.CommandText = "DELETE FROM files WHERE NOT EXISTS (SELECT episode.idFile FROM episode WHERE episode.idFile = files.idFile AND NOT episode.idFile = -1)"
                    SQLcommand.ExecuteNonQuery()
                End Using

                logger.Info("Removing seasons with no more existing episodes")
                Delete_Empty_TVSeasons(-1, True)
                logger.Info("Cleaning tv shows done")
            End If

            'global cleaning
            logger.Info("Cleaning global tables started")
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                'clean all link tables
                logger.Info("Cleaning actorlinkepisode table")
                SQLcommand.CommandText = "DELETE FROM actorlinkepisode WHERE NOT EXISTS (SELECT 1 FROM episode WHERE episode.idEpisode = actorlinkepisode.idEpisode)"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning actorlinkmovie table")
                SQLcommand.CommandText = "DELETE FROM actorlinkmovie WHERE NOT EXISTS (SELECT 1 FROM movie WHERE movie.idMovie = actorlinkmovie.idMovie)"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning actorlinktvshow table")
                SQLcommand.CommandText = "DELETE FROM actorlinktvshow WHERE NOT EXISTS (SELECT 1 FROM tvshow WHERE tvshow.idShow = actorlinktvshow.idShow)"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning countrylinkmovie table")
                SQLcommand.CommandText = "DELETE FROM countrylinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning directorlinkepisode table")
                SQLcommand.CommandText = "DELETE FROM directorlinkepisode WHERE idEpisode NOT IN (SELECT idEpisode FROM episode);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning directorlinkmovie table")
                SQLcommand.CommandText = "DELETE FROM directorlinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning directorlinktvshow table")
                SQLcommand.CommandText = "DELETE FROM directorlinktvshow WHERE idShow NOT IN (SELECT idShow FROM tvshow);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning genrelinkmovie table")
                SQLcommand.CommandText = "DELETE FROM genrelinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning genrelinktvshow table")
                SQLcommand.CommandText = "DELETE FROM genrelinktvshow WHERE idShow NOT IN (SELECT idShow FROM tvshow);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning setlinkmovie table")
                SQLcommand.CommandText = "DELETE FROM setlinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning studiolinkmovie table")
                SQLcommand.CommandText = "DELETE FROM studiolinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning studiolinktvshow table")
                SQLcommand.CommandText = "DELETE FROM studiolinktvshow WHERE idShow NOT IN (SELECT idShow FROM tvshow);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning writerlinkepisode table")
                SQLcommand.CommandText = "DELETE FROM writerlinkepisode WHERE idEpisode NOT IN (SELECT idEpisode FROM episode);"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning writerlinkmovie table")
                SQLcommand.CommandText = "DELETE FROM writerlinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
                SQLcommand.ExecuteNonQuery()
                'clean all main tables
                logger.Info("Cleaning genre table")
                SQLcommand.CommandText = String.Concat("DELETE FROM genre ",
                                                       "WHERE NOT EXISTS (SELECT 1 FROM genrelinkmovie WHERE genrelinkmovie.idGenre = genre.idGenre) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM genrelinktvshow WHERE genrelinktvshow.idGenre = genre.idGenre)")
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning actor table of actors, directors and writers")
                SQLcommand.CommandText = String.Concat("DELETE FROM actors ",
                                                       "WHERE NOT EXISTS (SELECT 1 FROM actorlinkmovie WHERE actorlinkmovie.idActor = actors.idActor) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM directorlinkmovie WHERE directorlinkmovie.idDirector = actors.idActor) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM writerlinkmovie WHERE writerlinkmovie.idWriter = actors.idActor) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM actorlinktvshow WHERE actorlinktvshow.idActor = actors.idActor) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM actorlinkepisode WHERE actorlinkepisode.idActor = actors.idActor) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM directorlinktvshow WHERE directorlinktvshow.idDirector = actors.idActor) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM directorlinkepisode WHERE directorlinkepisode.idDirector = actors.idActor) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM writerlinkepisode WHERE writerlinkepisode.idWriter = actors.idActor)")
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning country table")
                SQLcommand.CommandText = "DELETE FROM country WHERE NOT EXISTS (SELECT 1 FROM countrylinkmovie WHERE countrylinkmovie.idCountry = country.idCountry)"
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning genre table")
                SQLcommand.CommandText = String.Concat("DELETE FROM genre ",
                                                       "WHERE NOT EXISTS (SELECT 1 FROM genrelinkmovie WHERE genrelinkmovie.idGenre = genre.idGenre) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM genrelinktvshow WHERE genrelinktvshow.idGenre = genre.idGenre)")
                SQLcommand.ExecuteNonQuery()
                logger.Info("Cleaning studio table")
                SQLcommand.CommandText = String.Concat("DELETE FROM studio ",
                                                       "WHERE NOT EXISTS (SELECT 1 FROM studiolinkmovie WHERE studiolinkmovie.idStudio = studio.idStudio) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM studiolinktvshow WHERE studiolinktvshow.idStudio = studio.idStudio)")
                SQLcommand.ExecuteNonQuery()
            End Using
            logger.Info("Cleaning global tables done")

            SQLtransaction.Commit()
        End Using

        logger.Info("Cleaning videodatabase done")

        ' Housekeeping - consolidate and pack database using vacuum command http://www.sqlite.org/lang_vacuum.html
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            logger.Info("Rebulding videodatabase started")
            SQLcommand.CommandText = "VACUUM;"
            SQLcommand.ExecuteNonQuery()
            logger.Info("Rebulding videodatabase done")
        End Using
    End Sub

    Public Sub Cleanup_Genres()
        logger.Info("[Database] [Cleanup_Genres] Started")
        Dim MovieList As New List(Of Long)
        Dim TVShowList As New List(Of Long)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT DISTINCT idMovie FROM genrelinkmovie;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    MovieList.Add(Convert.ToInt64(SQLreader("idMovie")))
                End While
            End Using

            SQLcommand.CommandText = "SELECT DISTINCT idShow FROM genrelinktvshow;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    TVShowList.Add(Convert.ToInt64(SQLreader("idShow")))
                End While
            End Using
        End Using

        Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
            logger.Info("[Database] [Cleanup_Genres] Process all Movies")
            'Process all Movies, which are assigned to a genre
            For Each lMovieID In MovieList
                Dim tmpDBElement As DBElement = Load_Movie(lMovieID)
                If tmpDBElement.IsOnline Then
                    If StringUtils.GenreFilter(tmpDBElement.MainDetails.Genres, False) Then
                        Save_Movie(tmpDBElement, True, True, False, True, False)
                    End If
                Else
                    logger.Warn(String.Concat("[Database] [Cleanup_Genres] Skip Movie (not online): ", tmpDBElement.FileItem.FirstStackedPath))
                End If
            Next

            'Process all TVShows, which are assigned to a genre
            logger.Info("[Database] [Cleanup_Genres] Process all TVShows")
            For Each lTVShowID In TVShowList
                Dim tmpDBElement As DBElement = Load_TVShow(lTVShowID, False, False)
                If tmpDBElement.IsOnline Then
                    If StringUtils.GenreFilter(tmpDBElement.MainDetails.Genres, False) Then
                        Save_TVShow(tmpDBElement, True, True, False, False)
                    End If
                Else
                    logger.Warn(String.Concat("[Database] [Cleanup_Genres] Skip TV Show (not online): ", tmpDBElement.ShowPath))
                End If
            Next

            'Cleanup genre table
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                logger.Info("[Database] [Cleanup_Genres] Cleaning genre table")
                SQLcommand.CommandText = String.Concat("DELETE FROM genre ",
                                                       "WHERE NOT EXISTS (SELECT 1 FROM genrelinkmovie WHERE genrelinkmovie.idGenre = genre.idGenre) ",
                                                         "AND NOT EXISTS (SELECT 1 FROM genrelinktvshow WHERE genrelinktvshow.idGenre = genre.idGenre)")
                SQLcommand.ExecuteNonQuery()
            End Using

            SQLtransaction.Commit()
        End Using
        logger.Info("[Database] [Cleanup_Genres] Done")
    End Sub
    ''' <summary>
    ''' Remove the New flag from database entries (movies, tvshow, seasons, episode)
    ''' </summary>
    ''' <remarks>
    ''' 2013/12/13 Dekker500 - Check that MediaDBConn IsNot Nothing before continuing, 
    '''                        otherwise shutdown after a failed startup (before DB initialized) 
    '''                        will trow exception
    ''' </remarks>
    Public Sub ClearNew()
        If (Master.DB.MyVideosDBConn IsNot Nothing) Then
            Using SQLtransaction As SQLiteTransaction = Master.DB.MyVideosDBConn.BeginTransaction()
                Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand.CommandText = "UPDATE movie SET New = (?);"
                    Dim parNew As SQLiteParameter = SQLcommand.Parameters.Add("parNew", DbType.Boolean, 0, "New")
                    parNew.Value = False
                    SQLcommand.ExecuteNonQuery()
                End Using
                Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand.CommandText = "UPDATE sets SET New = (?);"
                    Dim parNew As SQLiteParameter = SQLcommand.Parameters.Add("parNew", DbType.Boolean, 0, "New")
                    parNew.Value = False
                    SQLcommand.ExecuteNonQuery()
                End Using
                Using SQLShowcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLShowcommand.CommandText = "UPDATE tvshow SET New = (?);"
                    Dim parShowNew As SQLiteParameter = SQLShowcommand.Parameters.Add("parShowNew", DbType.Boolean, 0, "New")
                    parShowNew.Value = False
                    SQLShowcommand.ExecuteNonQuery()
                End Using
                Using SQLSeasoncommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLSeasoncommand.CommandText = "UPDATE seasons SET New = (?);"
                    Dim parSeasonNew As SQLiteParameter = SQLSeasoncommand.Parameters.Add("parSeasonNew", DbType.Boolean, 0, "New")
                    parSeasonNew.Value = False
                    SQLSeasoncommand.ExecuteNonQuery()
                End Using
                Using SQLEpcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLEpcommand.CommandText = "UPDATE episode SET New = (?);"
                    Dim parEpNew As SQLiteParameter = SQLEpcommand.Parameters.Add("parEpNew", DbType.Boolean, 0, "New")
                    parEpNew.Value = False
                    SQLEpcommand.ExecuteNonQuery()
                End Using
                SQLtransaction.Commit()
            End Using
        End If
    End Sub
    ''' <summary>
    ''' Close the databases
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub Close_MyVideos()
        CloseDatabase(_myvideosDBConn)
        'CloseDatabase(_jobsDBConn)

        If _myvideosDBConn IsNot Nothing Then
            _myvideosDBConn = Nothing
        End If
        'If _jobsDBConn IsNot Nothing Then
        '    _jobsDBConn = Nothing
        'End If
    End Sub
    ''' <summary>
    ''' Perform the actual closing of the given database connection
    ''' </summary>
    ''' <param name="tConnection">Database connection on which to perform closing activities</param>
    ''' <remarks></remarks>
    Protected Sub CloseDatabase(ByRef tConnection As SQLiteConnection)
        If tConnection Is Nothing Then
            Return
        End If

        Try
            ' Housekeeping - consolidate and pack database using vacuum command http://www.sqlite.org/lang_vacuum.html
            Using command As SQLiteCommand = tConnection.CreateCommand()
                command.CommandText = "VACUUM;"
                command.ExecuteNonQuery()
            End Using

            tConnection.Close()
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name & Convert.ToChar(Windows.Forms.Keys.Tab) & "There was a problem closing the media database.")
        Finally
            tConnection.Dispose()
        End Try
    End Sub
    ''' <summary>
    ''' Creates the connection to the MediaDB database
    ''' </summary>
    ''' <returns><c>True</c> if the database needed to be created (is new), <c>False</c> otherwise</returns>
    ''' <remarks></remarks>
    Public Function Connect_MyVideos() As Boolean

        'set database version
        Dim MyVideosDBVersion As Integer = 47

        'set database filename
        Dim MyVideosDB As String = String.Format("MyVideos{0}.emm", MyVideosDBVersion)

        'TODO Warning - This method should be marked as Protected and references re-directed to Connect() above
        If _myvideosDBConn IsNot Nothing Then
            Return False
            'Throw New InvalidOperationException("A database connection is already open, can't open another.")
        End If

        Dim MyVideosDBFile As String = Path.Combine(Master.SettingsPath, MyVideosDB)

        'check if an older DB version still exist
        If Not File.Exists(MyVideosDBFile) Then
            For i As Integer = MyVideosDBVersion - 1 To 2 Step -1
                Dim oldMyVideosDB As String = String.Format("MyVideos{0}.emm", i)
                Dim oldMyVideosDBFile As String = Path.Combine(Master.SettingsPath, oldMyVideosDB)
                If File.Exists(oldMyVideosDBFile) Then
                    Master.fLoading.SetLoadingMesg(Master.eLang.GetString(1356, "Upgrading database..."))
                    Patch_MyVideos(oldMyVideosDBFile, MyVideosDBFile, i, MyVideosDBVersion)
                    Exit For
                End If
            Next
        End If

        Dim isNew As Boolean = Not File.Exists(MyVideosDBFile)

        Try
            _myvideosDBConn = New SQLiteConnection(String.Format(_connStringTemplate, MyVideosDBFile))
            _myvideosDBConn.Open()
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name & Convert.ToChar(Windows.Forms.Keys.Tab) & "Unable to open media database connection.")
        End Try

        Try
            If isNew Then
                Dim sqlCommand As String = File.ReadAllText(FileUtils.Common.ReturnSettingsFile("DB", "MyVideosDBSQL.txt"))

                Using transaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                    Using command As SQLiteCommand = _myvideosDBConn.CreateCommand()
                        command.CommandText = sqlCommand
                        command.ExecuteNonQuery()
                    End Using
                    transaction.Commit()
                End Using
            End If
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name & Convert.ToChar(Windows.Forms.Keys.Tab) & "Error creating database")
            Close_MyVideos()
            File.Delete(MyVideosDBFile)
        End Try
        Return isNew
    End Function
    ''' <summary>
    ''' Remove all empty TV Seasons there has no episodes defined
    ''' </summary>
    ''' <param name="lngShowID">Show ID</param>
    ''' <param name="bBatchMode">If <c>False</c>, the action is wrapped in a transaction</param>
    ''' <remarks></remarks>
    Public Sub Delete_Empty_TVSeasons(ByVal lngShowID As Long, ByVal bBatchMode As Boolean)
        Dim SQLtransaction As SQLiteTransaction = Nothing

        If Not bBatchMode Then SQLtransaction = Master.DB.MyVideosDBConn.BeginTransaction()
        Using SQLCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            If Not lngShowID = -1 Then
                SQLCommand.CommandText = String.Format("DELETE FROM seasons WHERE seasons.idShow = {0} AND NOT EXISTS (SELECT episode.Season FROM episode WHERE episode.Season = seasons.Season AND episode.idShow = seasons.idShow) AND seasons.Season <> 999", lngShowID)
            Else
                SQLCommand.CommandText = String.Format("DELETE FROM seasons WHERE NOT EXISTS (SELECT episode.Season FROM episode WHERE episode.Season = seasons.Season AND episode.idShow = seasons.idShow) AND seasons.Season <> 999")
            End If
            SQLCommand.ExecuteNonQuery()
        End Using
        If Not bBatchMode Then SQLtransaction.Commit()
        SQLtransaction = Nothing

        If SQLtransaction IsNot Nothing Then SQLtransaction.Dispose()
    End Sub
    ''' <summary>
    ''' Remove all TV Episodes they are no longer valid (not in <c>ValidEpisodes</c> list)
    ''' </summary>
    ''' <param name="bBatchMode">If <c>False</c>, the action is wrapped in a transaction</param>
    ''' <remarks></remarks>
    Public Sub Delete_Invalid_TVEpisodes(ByVal lstValidEpisodes As List(Of DBElement), ByVal lngShowID As Long, ByVal bBatchMode As Boolean)
        Dim SQLtransaction As SQLiteTransaction = Nothing

        If Not bBatchMode Then SQLtransaction = Master.DB.MyVideosDBConn.BeginTransaction()
        Using SQLCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLCommand.CommandText = String.Format("SELECT idEpisode FROM episode WHERE idShow = {0};", lngShowID)
            Using SQLreader As SQLiteDataReader = SQLCommand.ExecuteReader()
                While SQLreader.Read
                    If lstValidEpisodes.Where(Function(f) f.ID = Convert.ToInt64(SQLreader("idEpisode"))).Count = 0 Then
                        Delete_TVEpisode(Convert.ToInt64(SQLreader("idEpisode")), True, True)
                    End If
                End While
            End Using
        End Using
        If Not bBatchMode Then SQLtransaction.Commit()
        SQLtransaction = Nothing

        If SQLtransaction IsNot Nothing Then SQLtransaction.Dispose()
    End Sub
    ''' <summary>
    ''' Remove all TV Seasons they are no longer valid (not in <c>ValidSeasons</c> list)
    ''' </summary>
    ''' <param name="bBatchMode">If <c>False</c>, the action is wrapped in a transaction</param>
    ''' <remarks></remarks>
    Public Sub Delete_Invalid_TVSeasons(ByVal lstValidSeasons As List(Of DBElement), ByVal lngShowID As Long, ByVal bBatchMode As Boolean)
        Dim SQLtransaction As SQLiteTransaction = Nothing

        If Not bBatchMode Then SQLtransaction = Master.DB.MyVideosDBConn.BeginTransaction()
        Using SQLCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLCommand.CommandText = String.Format("SELECT idSeason FROM seasons WHERE idShow = {0};", lngShowID)
            Using SQLreader As SQLiteDataReader = SQLCommand.ExecuteReader()
                While SQLreader.Read
                    If lstValidSeasons.Where(Function(f) f.ID = Convert.ToInt64(SQLreader("idSeason"))).Count = 0 Then
                        Delete_TVSeason(Convert.ToInt64(SQLreader("idSeason")), True)
                    End If
                End While
            End Using
        End Using
        If Not bBatchMode Then SQLtransaction.Commit()
        SQLtransaction = Nothing

        If SQLtransaction IsNot Nothing Then SQLtransaction.Dispose()
    End Sub

    ''' <summary>
    ''' Remove all information related to a movie from the database.
    ''' </summary>
    ''' <param name="lngID">ID of the movie to remove, as stored in the database.</param>
    ''' <param name="bBatchMode">Is this function already part of a transaction?</param>
    ''' <returns>True if successful, false if deletion failed.</returns>
    Public Function Delete_Movie(ByVal lngID As Long, ByVal bBatchMode As Boolean) As Boolean
        If lngID < 0 Then Throw New ArgumentOutOfRangeException("idMovie", "Value must be >= 0, was given: " & lngID)

        Dim _movieDB As DBElement = Load_Movie(lngID)
        AddonsManager.Instance.RunGeneric(Enums.AddonEventType.Remove_Movie, Nothing, Nothing, False, _movieDB)

        Try
            Dim SQLtransaction As SQLiteTransaction = Nothing
            If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("DELETE FROM movie WHERE idMovie = ", lngID, ";")
                SQLcommand.ExecuteNonQuery()
            End Using
            If Not bBatchMode Then SQLtransaction.Commit()

            RaiseEvent GenericEvent(Enums.AddonEventType.Remove_Movie, New List(Of Object)(New Object() {_movieDB.ID}))
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
            Return False
        End Try
        Return True
    End Function
    ''' <summary>
    ''' Remove all information related to a movieset from the database.
    ''' </summary>
    ''' <param name="lngID">ID of the movieset to remove, as stored in the database.</param>
    ''' <param name="bBatchMode">Is this function already part of a transaction?</param>
    ''' <returns>True if successful, false if deletion failed.</returns>
    Public Function Delete_MovieSet(ByVal lngID As Long, ByVal bBatchMode As Boolean) As Boolean
        Try
            'first get a list of all movies in the movieset to remove the movieset information from NFO
            Dim moviesToSave As New List(Of DBElement)

            Dim SQLtransaction As SQLiteTransaction = Nothing
            If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("SELECT idMovie FROM setlinkmovie ",
                                                       "WHERE idSet = ", lngID, ";")
                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                    While SQLreader.Read
                        If Not DBNull.Value.Equals(SQLreader("idMovie")) Then
                            moviesToSave.Add(Load_Movie(Convert.ToInt64(SQLreader("idMovie"))))
                        End If
                    End While
                End Using
            End Using

            'remove the movieset from movie and write new movie NFOs
            If moviesToSave.Count > 0 Then
                For Each movie In moviesToSave
                    movie.MainDetails.RemoveSet(lngID)
                    Save_Movie(movie, bBatchMode, True, False, True, False)
                Next
            End If

            'delete all movieset images and if this setting is enabled
            If Master.eSettings.MovieSetCleanFiles Then
                Dim MovieSet As DBElement = Master.DB.Load_MovieSet(lngID)
                Images.Delete_MovieSet(MovieSet, Enums.ScrapeModifierType.MainBanner)
                Images.Delete_MovieSet(MovieSet, Enums.ScrapeModifierType.MainClearArt)
                Images.Delete_MovieSet(MovieSet, Enums.ScrapeModifierType.MainClearLogo)
                Images.Delete_MovieSet(MovieSet, Enums.ScrapeModifierType.MainDiscArt)
                Images.Delete_MovieSet(MovieSet, Enums.ScrapeModifierType.MainFanart)
                Images.Delete_MovieSet(MovieSet, Enums.ScrapeModifierType.MainLandscape)
                Images.Delete_MovieSet(MovieSet, Enums.ScrapeModifierType.MainPoster)
            End If

            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("SELECT idMovie FROM setlinkmovie ",
                                                       "WHERE idSet = ", lngID, ";")
                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                    While SQLreader.Read
                        If Not DBNull.Value.Equals(SQLreader("idMovie")) Then
                            moviesToSave.Add(Load_Movie(Convert.ToInt64(SQLreader("idMovie"))))
                        End If
                    End While
                End Using
            End Using

            'remove the movieset and still existing setlinkmovie entries
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("DELETE FROM sets WHERE idSet = ", lngID, ";")
                SQLcommand.ExecuteNonQuery()
            End Using
            If Not bBatchMode Then SQLtransaction.Commit()
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
            Return False
        End Try
        Return True
    End Function

    ''' <summary>
    ''' Remove all information related to a tag from the database.
    ''' </summary>
    ''' <param name="lngID">Internal TagID of the tag to remove, as stored in the database.</param>
    ''' <param name="intMode">1=tag of a movie, 2=tag of a show</param>
    ''' <param name="bBatchMode">Is this function already part of a transaction?</param>
    ''' <returns>True if successful, false if deletion failed.</returns>
    Public Function Delete_Tag(ByVal lngID As Long, ByVal intMode As Integer, ByVal bBatchMode As Boolean) As Boolean
        Try
            'first get a list of all movies in the tag to remove the tag information from NFO
            Dim moviesToSave As New List(Of DBElement)
            Dim SQLtransaction As SQLiteTransaction = Nothing
            Dim tagName As String = String.Empty
            If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("SELECT strTag FROM tag ",
                                                       "WHERE idTag = ", lngID, ";")
                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                    While SQLreader.Read
                        If Not DBNull.Value.Equals(SQLreader("strTag")) Then tagName = CStr(SQLreader("strTag"))
                    End While
                End Using
            End Using

            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("SELECT idMedia FROM taglinks ",
                                                       "WHERE idTag = ", lngID, ";")
                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                    While SQLreader.Read
                        If intMode = 1 Then
                            'tag is for movie
                            If Not DBNull.Value.Equals(SQLreader("idMedia")) Then
                                moviesToSave.Add(Load_Movie(Convert.ToInt64(SQLreader("idMedia"))))
                            End If
                        End If
                    End While
                End Using
            End Using

            'remove the tag from movie and write new movie NFOs
            If moviesToSave.Count > 0 Then
                For Each movie In moviesToSave
                    movie.MainDetails.Tags.Remove(tagName)
                    Save_Movie(movie, bBatchMode, True, False, True, False)
                Next
            End If

            'remove the tag entry
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("DELETE FROM tag WHERE idTag = ", lngID, ";")
                SQLcommand.ExecuteNonQuery()
            End Using
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("DELETE FROM taglinks WHERE idTag = ", lngID, ";")
                SQLcommand.ExecuteNonQuery()
            End Using
            If Not bBatchMode Then SQLtransaction.Commit()
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
            Return False
        End Try
        Return True
    End Function

    ''' <summary>
    ''' Remove all information related to a TV episode from the database.
    ''' </summary>
    ''' <param name="lngTVEpisodeID">ID of the episode to remove, as stored in the database.</param>
    ''' <param name="bBatchMode">Is this function already part of a transaction?</param>
    ''' <returns><c>True</c> if has been removed, <c>False</c> if has been changed to missing</returns>
    Public Function Delete_TVEpisode(ByVal lngTVEpisodeID As Long, ByVal bBatchMode As Boolean, ByVal bForce As Boolean) As Boolean
        Dim SQLtransaction As SQLiteTransaction = Nothing
        Dim doesExist As Boolean = False
        Dim bHasRemoved As Boolean = False

        Dim _tvepisodeDB As DBElement = Load_TVEpisode(lngTVEpisodeID, True)
        AddonsManager.Instance.RunGeneric(Enums.AddonEventType.Remove_TVEpisode, Nothing, Nothing, False, _tvepisodeDB)

        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT idFile, Episode, Season, idShow FROM episode WHERE idEpisode = ", lngTVEpisodeID, ";")
            Using SQLReader As SQLiteDataReader = SQLcommand.ExecuteReader
                While SQLReader.Read
                    Using SQLECommand As SQLiteCommand = _myvideosDBConn.CreateCommand()

                        If Not bForce Then
                            'check if there is another episode with same season and episode number (in this case we don't need a another "Missing" episode)
                            Using SQLcommand_select As SQLiteCommand = _myvideosDBConn.CreateCommand
                                SQLcommand_select.CommandText = String.Format("SELECT COUNT(episode.idEpisode) AS eCount FROM episode WHERE NOT idEpisode = {0} AND Season = {1} AND Episode = {2} AND idShow = {3}", lngTVEpisodeID, SQLReader("Season"), SQLReader("Episode"), SQLReader("idShow"))
                                Using SQLReader_select As SQLiteDataReader = SQLcommand_select.ExecuteReader
                                    While SQLReader_select.Read
                                        If CInt(SQLReader_select("eCount")) > 0 Then doesExist = True
                                    End While
                                End Using
                            End Using
                        End If

                        If bForce OrElse doesExist Then
                            SQLECommand.CommandText = String.Concat("DELETE FROM episode WHERE idEpisode = ", lngTVEpisodeID, ";")
                            SQLECommand.ExecuteNonQuery()
                            bHasRemoved = True
                        ElseIf Not Convert.ToInt64(SQLReader("idFile")) = -1 Then 'already marked as missing, no need for another query
                            'check if there is another episode that use the same idFile
                            Dim multiEpisode As Boolean = False
                            Using SQLcommand_select As SQLiteCommand = _myvideosDBConn.CreateCommand
                                SQLcommand_select.CommandText = String.Format("SELECT COUNT(episode.idFile) AS eCount FROM episode WHERE idFile = {0}", Convert.ToInt64(SQLReader("idFile")))
                                Using SQLReader_select As SQLiteDataReader = SQLcommand_select.ExecuteReader
                                    While SQLReader_select.Read
                                        If CInt(SQLReader_select("eCount")) > 1 Then multiEpisode = True
                                    End While
                                End Using
                            End Using
                            If Not multiEpisode Then
                                SQLECommand.CommandText = String.Concat("DELETE FROM files WHERE idFile = ", Convert.ToInt64(SQLReader("idFile")), ";")
                                SQLECommand.ExecuteNonQuery()
                            End If
                            SQLECommand.CommandText = String.Concat("DELETE FROM TVVStreams WHERE TVEpID = ", lngTVEpisodeID, ";")
                            SQLECommand.ExecuteNonQuery()
                            SQLECommand.CommandText = String.Concat("DELETE FROM TVAStreams WHERE TVEpID = ", lngTVEpisodeID, ";")
                            SQLECommand.ExecuteNonQuery()
                            SQLECommand.CommandText = String.Concat("DELETE FROM TVSubs WHERE TVEpID = ", lngTVEpisodeID, ";")
                            SQLECommand.ExecuteNonQuery()
                            SQLECommand.CommandText = String.Concat("DELETE FROM art WHERE media_id = ", lngTVEpisodeID, " AND media_type = 'episode';")
                            SQLECommand.ExecuteNonQuery()
                            SQLECommand.CommandText = String.Concat("UPDATE episode SET New = 0, ",
                                                                    "idFile = -1, NfoPath = '', ",
                                                                    "VideoSource = '' WHERE idEpisode = ", lngTVEpisodeID, ";")
                            SQLECommand.ExecuteNonQuery()
                        End If
                    End Using
                End While
            End Using
        End Using

        If Not bBatchMode Then
            SQLtransaction.Commit()
        End If

        Return bHasRemoved
    End Function

    Public Function Delete_TVEpisode(ByVal strPath As String, ByVal bForce As Boolean, ByVal bBatchMode As Boolean) As Boolean
        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
        Using SQLPCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLPCommand.CommandText = String.Concat("SELECT idFile FROM files WHERE strFilename = """, strPath, """;")
            Using SQLPReader As SQLiteDataReader = SQLPCommand.ExecuteReader
                While SQLPReader.Read
                    Using SQLCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                        SQLCommand.CommandText = String.Concat("SELECT idEpisode FROM episode WHERE idFile = ", SQLPReader("idFile"), ";")
                        Using SQLReader As SQLiteDataReader = SQLCommand.ExecuteReader
                            While SQLReader.Read
                                Delete_TVEpisode(CInt(SQLReader("idEpisode")), bBatchMode, bForce)
                            End While
                        End Using
                    End Using
                End While
            End Using
        End Using
        If Not bBatchMode Then SQLtransaction.Commit()

        Return True
    End Function

    ''' <summary>
    ''' Remove all information related to a TV season from the database.
    ''' </summary>
    ''' <param name="bBatchMode">Is this function already part of a transaction?</param>
    ''' <returns>True if successful, false if deletion failed.</returns>
    Public Function Delete_TVSeason(ByVal lngID As Long, ByVal bBatchMode As Boolean) As Boolean
        If lngID < 0 Then Throw New ArgumentOutOfRangeException("idSeason", "Value must be >= 0, was given: " & lngID)

        Try
            Dim SQLtransaction As SQLiteTransaction = Nothing
            If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("DELETE FROM seasons WHERE idSeason = ", lngID, ";")
                SQLcommand.ExecuteNonQuery()
            End Using
            If Not bBatchMode Then SQLtransaction.Commit()
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
            Return False
        End Try
        Return True
    End Function

    ''' <summary>
    ''' Remove all information related to a TV season from the database.
    ''' </summary>
    ''' <param name="lngShowID">ID of the tvshow to remove, as stored in the database.</param>
    ''' <param name="bBatchMode">Is this function already part of a transaction?</param>
    ''' <returns>True if successful, false if deletion failed.</returns>
    Public Function Delete_TVSeason(ByVal lngShowID As Long, ByVal intSeason As Integer, ByVal bBatchMode As Boolean) As Boolean
        If lngShowID < 0 Then Throw New ArgumentOutOfRangeException("ShowID", "Value must be >= 0, was given: " & lngShowID)
        If intSeason < 0 Then Throw New ArgumentOutOfRangeException("iSeason", "Value must be >= 0, was given: " & intSeason)

        Try
            Dim SQLtransaction As SQLiteTransaction = Nothing
            If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("DELETE FROM seasons WHERE idShow = ", lngShowID, " AND Season = ", intSeason, ";")
                SQLcommand.ExecuteNonQuery()
            End Using
            If Not bBatchMode Then SQLtransaction.Commit()
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
            Return False
        End Try
        Return True
    End Function

    ''' <summary>
    ''' Remove all information related to a TV show from the database.
    ''' </summary>
    ''' <param name="lngID">ID of the tvshow to remove, as stored in the database.</param>
    ''' <param name="bBatchMode">Is this function already part of a transaction?</param>
    ''' <returns>True if successful, false if deletion failed.</returns>
    Public Function Delete_TVShow(ByVal lngID As Long, ByVal bBatchMode As Boolean) As Boolean
        If lngID < 0 Then Throw New ArgumentOutOfRangeException("idShow", "Value must be >= 0, was given: " & lngID)

        Dim _tvshowDB As Database.DBElement = Load_TVShow_Full(lngID)
        AddonsManager.Instance.RunGeneric(Enums.AddonEventType.Remove_TVShow, Nothing, Nothing, False, _tvshowDB)

        Try
            Dim SQLtransaction As SQLiteTransaction = Nothing
            If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("DELETE FROM tvshow WHERE idShow = ", lngID, ";")
                SQLcommand.ExecuteNonQuery()
            End Using
            If Not bBatchMode Then SQLtransaction.Commit()

            RaiseEvent GenericEvent(Enums.AddonEventType.Remove_TVShow, New List(Of Object)(New Object() {_tvshowDB.ID}))
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
            Return False
        End Try
        Return True
    End Function

    ''' <summary>
    ''' Fill DataTable with data returned from the provided command
    ''' </summary>
    ''' <param name="tTable">DataTable to fill</param>
    ''' <param name="strCommand">SQL Command to process</param>
    Public Sub FillDataTable(ByRef tTable As DataTable, ByVal strCommand As String)
        tTable.Clear()
        Dim sqlDA As New SQLiteDataAdapter(strCommand, _myvideosDBConn)
        sqlDA.Fill(tTable)
    End Sub
    ''' <summary>
    ''' Adds TVShow informations to a Database.DBElement
    ''' </summary>
    ''' <param name="tDBElement">Database.DBElement container to fill with TVShow informations</param>
    ''' <param name="tDBElement_TVShow">Optional the TVShow informations to add to _TVDB</param>
    ''' <remarks></remarks>
    Public Function AddTVShowInfoToDBElement(ByVal tDBElement As DBElement, Optional ByVal tDBElement_TVShow As DBElement = Nothing) As DBElement
        Dim _tmpTVDBShow As DBElement

        If tDBElement_TVShow Is Nothing OrElse tDBElement_TVShow.MainDetails Is Nothing Then
            _tmpTVDBShow = Load_TVShow(tDBElement.ShowID, False, False)
        Else
            _tmpTVDBShow = tDBElement_TVShow
        End If

        tDBElement.EpisodeSorting = _tmpTVDBShow.EpisodeSorting
        tDBElement.Ordering = _tmpTVDBShow.Ordering
        tDBElement.Language = _tmpTVDBShow.Language
        tDBElement.ShowID = _tmpTVDBShow.ShowID
        tDBElement.ShowPath = _tmpTVDBShow.ShowPath
        tDBElement.Source = _tmpTVDBShow.Source
        tDBElement.TVShowDetails = _tmpTVDBShow.MainDetails
        Return tDBElement
    End Function

    Public Function GetAll_MovieSetDetails() As List(Of MediaContainers.SetDetails)
        Dim tList As New List(Of MediaContainers.SetDetails)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT idSet FROM sets ORDER BY SetName;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Dim nMovieSet = Load_MovieSet(CLng(SQLreader("idSet")))
                    tList.Add(New MediaContainers.SetDetails With {
                              .ID = nMovieSet.ID,
                              .Plot = nMovieSet.MainDetails.Plot,
                              .Title = nMovieSet.MainDetails.Title,
                              .TMDB = nMovieSet.MainDetails.TMDB})
                End While
            End Using
        End Using

        Return tList
    End Function

    Public Function GetAll_MovieSets() As List(Of MediaContainers.MainDetails)
        Dim tList As New List(Of MediaContainers.MainDetails)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT idSet FROM sets ORDER BY SetName;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    tList.Add(Load_MovieSet(CLng(SQLreader("idSet"))).MainDetails)
                End While
            End Using
        End Using

        Return tList
    End Function

    Public Function GetAll_MovieSetTitles() As List(Of String)
        Dim tList As New List(Of String)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT SetName FROM sets;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not String.IsNullOrEmpty(SQLreader("SetName").ToString) Then
                        If Not tList.Contains(SQLreader("SetName").ToString) Then
                            tList.Add(SQLreader("SetName").ToString.Trim)
                        End If
                    End If
                End While
            End Using
        End Using

        tList.Sort()
        Return tList
    End Function

    Public Function GetAll_Tags() As String()
        Dim tList As New List(Of String)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT strTag FROM tag;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not String.IsNullOrEmpty(SQLreader("strTag").ToString) Then
                        If Not tList.Contains(SQLreader("strTag").ToString) Then
                            tList.Add(SQLreader("strTag").ToString.Trim)
                        End If
                    End If
                End While
            End Using
        End Using

        tList.Sort()
        Return tList.ToArray
    End Function

    Public Function GetAll_TVShowTitles() As String()
        Dim tList As New List(Of String)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT Title FROM tvshow;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not String.IsNullOrEmpty(SQLreader("Title").ToString) Then
                        If Not tList.Contains(SQLreader("Title").ToString) Then
                            tList.Add(SQLreader("Title").ToString.Trim)
                        End If
                    End If
                End While
            End Using
        End Using

        tList.Sort()
        Return tList.ToArray
    End Function

    Public Function GetTVShowEpisodeSorting(ByVal lngShowID As Long) As Enums.EpisodeSorting
        Dim sEpisodeSorting As Enums.EpisodeSorting = Enums.EpisodeSorting.Episode

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT EpisodeSorting FROM tvshow WHERE idShow = ", lngShowID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    sEpisodeSorting = DirectCast(Convert.ToInt32(SQLreader("EpisodeSorting")), Enums.EpisodeSorting)
                End While
            End Using
        End Using

        Return sEpisodeSorting
    End Function

    Public Function GetMovieCountries() As String()
        Dim cList As New List(Of String)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT strCountry FROM country;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    cList.Add(SQLreader("strCountry").ToString)
                End While
            End Using
        End Using

        cList.Sort()
        Return cList.ToArray
    End Function

    Public Sub LoadAllGenres()
        Dim gList As New List(Of String)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT strGenre FROM genre;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    gList.Add(SQLreader("strGenre").ToString)
                End While
            End Using
        End Using

        For Each tGenre As String In gList
            Dim gMapping As genreMapping = APIXML.GenreXML.Mappings.FirstOrDefault(Function(f) f.SearchString = tGenre)
            If gMapping Is Nothing Then
                'check if the tGenre is already existing in Gernes list
                Dim gProperty As genreProperty = APIXML.GenreXML.Genres.FirstOrDefault(Function(f) f.Name = tGenre)
                If gProperty Is Nothing Then
                    APIXML.GenreXML.Genres.Add(New genreProperty With {.isNew = False, .Name = tGenre})
                End If
                'add a new mapping if tGenre is not in the MappingTable
                APIXML.GenreXML.Mappings.Add(New genreMapping With {.isNew = False, .MappedTo = New List(Of String) From {tGenre}, .SearchString = tGenre})
            End If
        Next
        APIXML.GenreXML.Save()
    End Sub

    Public Function GetAll_MoviePaths() As List(Of String)
        Dim tList As New List(Of String)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT MoviePath FROM movie;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    tList.Add(SQLreader("MoviePath").ToString.ToLower)
                End While
            End Using
        End Using

        Return tList
    End Function

    Public Function GetAll_TVEpisodePaths() As List(Of String)
        Dim tList As New List(Of String)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT strFilename FROM files;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    tList.Add(SQLreader("strFilename").ToString.ToLower)
                End While
            End Using
        End Using

        Return tList
    End Function

    Public Function GetAll_TVShowPaths() As Hashtable
        Dim tList As New Hashtable

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT idShow, TVShowPath FROM tvshow;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    tList.Add(SQLreader("TVShowPath").ToString.ToLower, SQLreader("idShow"))
                End While
            End Using
        End Using

        Return tList
    End Function

    Public Function GetTVSeasonIDFromEpisode(ByVal tDBElement As DBElement) As Long
        Dim sID As Long = -1
        If tDBElement.MainDetails IsNot Nothing Then
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Format("SELECT idSeason FROM seasons WHERE idShow = {0} AND Season = {1};", tDBElement.ShowID, tDBElement.MainDetails.Season)
                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                    If SQLreader.HasRows Then
                        SQLreader.Read()
                        sID = CLng(SQLreader.Item("idSeason"))
                        Return sID
                    Else
                        Return sID
                    End If
                End Using
            End Using
        Else
            Return sID
        End If
    End Function

    Public Function AddView(ByVal strCommand As String) As Boolean
        Try
            Dim SQLtransaction As SQLiteTransaction = Nothing
            SQLtransaction = _myvideosDBConn.BeginTransaction()
            Using SQLcommand_view_add As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand_view_add.CommandText = strCommand
                SQLcommand_view_add.ExecuteNonQuery()
            End Using
            SQLtransaction.Commit()
            Return True
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
            Return False
        End Try
    End Function

    Public Function DeleteView(ByVal strViewName As String) As Boolean
        If String.IsNullOrEmpty(strViewName) Then Return False
        Try
            Dim SQLtransaction As SQLiteTransaction = Nothing
            SQLtransaction = _myvideosDBConn.BeginTransaction()
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("DROP VIEW IF EXISTS """, strViewName, """;")
                SQLcommand.ExecuteNonQuery()
            End Using
            SQLtransaction.Commit()
            Return True
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
            Return False
        End Try
    End Function

    Public Function GetViewDetails(ByVal strViewName As String) As SQLViewProperty
        Dim ViewProperty As New SQLViewProperty
        If Not String.IsNullOrEmpty(strViewName) Then
            Try
                Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand.CommandText = String.Concat("SELECT name, sql FROM sqlite_master WHERE type ='view' AND name='", strViewName, "';")
                    Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                        While SQLreader.Read
                            ViewProperty.Name = SQLreader("name").ToString
                            ViewProperty.Statement = SQLreader("sql").ToString
                        End While
                    End Using
                End Using
                Return ViewProperty
            Catch ex As Exception
                logger.Error(ex, New StackFrame().GetMethod().Name)
            End Try
        End If
        Return ViewProperty
    End Function

    Public Function ViewExists(ByVal strViewName As String) As Boolean
        If Not String.IsNullOrEmpty(strViewName) Then
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Format("SELECT name FROM sqlite_master WHERE type ='view' AND name = '{0}';", strViewName)
                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                    If SQLreader.HasRows Then
                        Return True
                    Else
                        Return False
                    End If
                End Using
            End Using
        Else
            Return False
        End If
    End Function

    Public Function GetViewMediaCount(ByVal strViewName As String, Optional bEpisodesByView As Boolean = False) As Integer
        Dim mCount As Integer
        If Not String.IsNullOrEmpty(strViewName) Then
            If Not bEpisodesByView Then
                Using SQLCommand As SQLiteCommand = Master.DB.MyVideosDBConn.CreateCommand()
                    SQLCommand.CommandText = String.Format("SELECT COUNT(*) FROM '{0}'", strViewName)
                    mCount = Convert.ToInt32(SQLCommand.ExecuteScalar)
                    Return mCount
                End Using
            Else
                Using SQLCommand As SQLiteCommand = Master.DB.MyVideosDBConn.CreateCommand()
                    SQLCommand.CommandText = String.Format("SELECT COUNT(*) FROM '{0}' INNER JOIN episode ON ('{0}'.idShow = episode.idShow) WHERE NOT episode.idFile = -1", strViewName)
                    mCount = Convert.ToInt32(SQLCommand.ExecuteScalar)
                    Return mCount
                End Using
            End If
        Else
            Return mCount
        End If
    End Function

    Public Function GetViewList(ByVal eContentType As Enums.ContentType) As List(Of String)
        Dim ViewList As New List(Of String)
        Dim ContentType As String = String.Empty

        Select Case eContentType
            Case Enums.ContentType.TVEpisode
                ContentType = "episode-"
            Case Enums.ContentType.Movie
                ContentType = "movie-"
            Case Enums.ContentType.Movieset
                ContentType = "sets-"
            Case Enums.ContentType.TVSeason
                ContentType = "seasons-"
            Case Enums.ContentType.TVShow
                ContentType = "tvshow-"
        End Select

        If Not String.IsNullOrEmpty(ContentType) OrElse eContentType = Enums.ContentType.None Then
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Format("SELECT name FROM sqlite_master WHERE type ='view' AND name LIKE '{0}%';", ContentType)
                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                    While SQLreader.Read
                        ViewList.Add(SQLreader("name").ToString)
                    End While
                End Using
            End Using

            'remove default lists
            If ViewList.Contains("episodelist") Then ViewList.Remove("episodelist")
            If ViewList.Contains("movielist") Then ViewList.Remove("movielist")
            If ViewList.Contains("seasonslist") Then ViewList.Remove("seasonslist")
            If ViewList.Contains("setslist") Then ViewList.Remove("setslist")
            If ViewList.Contains("tvshowlist") Then ViewList.Remove("tvshowlist")
        End If

        Return ViewList
    End Function
    ''' <summary>
    ''' Load excluded directories from the DB. This populates the Master.ExcludeDirs list
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub Load_ExcludeDirs()
        Master.ExcludedDirs.Clear()
        Try
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = "SELECT Dirname FROM ExcludeDir;"
                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                    While SQLreader.Read
                        Try ' Parsing database entry may fail. If it does, log the error and ignore the entry but continue processing
                            Dim eDir As String = String.Empty
                            eDir = SQLreader("Dirname").ToString
                            Master.ExcludedDirs.Add(eDir)
                        Catch ex As Exception
                            logger.Error(ex, New StackFrame().GetMethod().Name)
                        End Try
                    End While
                End Using
            End Using
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
        End Try
    End Sub
    ''' <summary>
    ''' Load all the information for a movie.
    ''' </summary>
    ''' <param name="lngMovieID">ID of the movie to load, as stored in the database</param>
    ''' <returns>Database.DBElement object</returns>
    Public Function Load_Movie(ByVal lngMovieID As Long) As DBElement
        Dim _movieDB As New DBElement(Enums.ContentType.Movie)
        _movieDB.MainDetails = New MediaContainers.MainDetails

        _movieDB.ID = lngMovieID
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM movie WHERE idMovie = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.HasRows Then
                    SQLreader.Read()
                    If Not DBNull.Value.Equals(SQLreader("DateAdded")) Then _movieDB.DateAdded = Convert.ToInt64(SQLreader("DateAdded"))
                    If Not DBNull.Value.Equals(SQLreader("DateModified")) Then _movieDB.DateModified = Convert.ToInt64(SQLreader("DateModified"))
                    If Not DBNull.Value.Equals(SQLreader("ListTitle")) Then _movieDB.ListTitle = SQLreader("ListTitle").ToString
                    If Not DBNull.Value.Equals(SQLreader("MoviePath")) Then _movieDB.FileItem = New FileItem(SQLreader("MoviePath").ToString)
                    _movieDB.IsSingle = Convert.ToBoolean(SQLreader("Type"))
                    If Not DBNull.Value.Equals(SQLreader("TrailerPath")) Then _movieDB.Trailer.LocalFilePath = SQLreader("TrailerPath").ToString
                    If Not DBNull.Value.Equals(SQLreader("NfoPath")) Then _movieDB.NfoPath = SQLreader("NfoPath").ToString
                    If Not DBNull.Value.Equals(SQLreader("EThumbsPath")) Then _movieDB.ExtrathumbsPath = SQLreader("EThumbsPath").ToString
                    If Not DBNull.Value.Equals(SQLreader("EFanartsPath")) Then _movieDB.ExtrafanartsPath = SQLreader("EFanartsPath").ToString
                    If Not DBNull.Value.Equals(SQLreader("ThemePath")) Then _movieDB.Theme.LocalFilePath = SQLreader("ThemePath").ToString

                    _movieDB.Source = Load_Source_Movie(Convert.ToInt64(SQLreader("idSource")))

                    _movieDB.IsMark = Convert.ToBoolean(SQLreader("Mark"))
                    _movieDB.IsLock = Convert.ToBoolean(SQLreader("Lock"))
                    _movieDB.OutOfTolerance = Convert.ToBoolean(SQLreader("OutOfTolerance"))
                    _movieDB.IsMarkCustom1 = Convert.ToBoolean(SQLreader("MarkCustom1"))
                    _movieDB.IsMarkCustom2 = Convert.ToBoolean(SQLreader("MarkCustom2"))
                    _movieDB.IsMarkCustom3 = Convert.ToBoolean(SQLreader("MarkCustom3"))
                    _movieDB.IsMarkCustom4 = Convert.ToBoolean(SQLreader("MarkCustom4"))
                    If Not DBNull.Value.Equals(SQLreader("VideoSource")) Then _movieDB.VideoSource = SQLreader("VideoSource").ToString
                    If Not DBNull.Value.Equals(SQLreader("Language")) Then _movieDB.Language = SQLreader("Language").ToString

                    With _movieDB.MainDetails
                        If Not DBNull.Value.Equals(SQLreader("DateAdded")) Then .DateAdded = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(SQLreader("DateAdded"))).ToString("yyyy-MM-dd HH:mm:ss")
                        If Not DBNull.Value.Equals(SQLreader("DateModified")) Then .DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(SQLreader("DateModified"))).ToString("yyyy-MM-dd HH:mm:ss")
                        If Not DBNull.Value.Equals(SQLreader("IMDB")) Then .IMDB = SQLreader("IMDB").ToString
                        If Not DBNull.Value.Equals(SQLreader("Title")) Then .Title = SQLreader("Title").ToString
                        If Not DBNull.Value.Equals(SQLreader("OriginalTitle")) Then .OriginalTitle = SQLreader("OriginalTitle").ToString
                        If Not DBNull.Value.Equals(SQLreader("SortTitle")) Then .SortTitle = SQLreader("SortTitle").ToString
                        If Not DBNull.Value.Equals(SQLreader("Year")) Then .Year = SQLreader("Year").ToString
                        If Not DBNull.Value.Equals(SQLreader("Rating")) Then .Rating = SQLreader("Rating").ToString
                        If Not DBNull.Value.Equals(SQLreader("Votes")) Then .Votes = SQLreader("Votes").ToString
                        If Not DBNull.Value.Equals(SQLreader("MPAA")) Then .MPAA = SQLreader("MPAA").ToString
                        If Not DBNull.Value.Equals(SQLreader("Top250")) Then .Top250 = Convert.ToInt32(SQLreader("Top250"))
                        If Not DBNull.Value.Equals(SQLreader("Outline")) Then .Outline = SQLreader("Outline").ToString
                        If Not DBNull.Value.Equals(SQLreader("Plot")) Then .Plot = SQLreader("Plot").ToString
                        If Not DBNull.Value.Equals(SQLreader("Tagline")) Then .Tagline = SQLreader("Tagline").ToString
                        If Not DBNull.Value.Equals(SQLreader("Trailer")) Then .Trailer = SQLreader("Trailer").ToString
                        If Not DBNull.Value.Equals(SQLreader("Certification")) Then .AddCertificationsFromString(SQLreader("Certification").ToString)
                        If Not DBNull.Value.Equals(SQLreader("Runtime")) Then .Runtime = SQLreader("Runtime").ToString
                        If Not DBNull.Value.Equals(SQLreader("ReleaseDate")) Then .ReleaseDate = SQLreader("ReleaseDate").ToString
                        If Not DBNull.Value.Equals(SQLreader("PlayCount")) Then .PlayCount = Convert.ToInt32(SQLreader("PlayCount"))
                        If Not DBNull.Value.Equals(SQLreader("FanartURL")) AndAlso Not Master.eSettings.Movie.ImageSettings.ImagesNotSaveURLToNfo Then .Fanart.URL = SQLreader("FanartURL").ToString
                        If Not DBNull.Value.Equals(SQLreader("VideoSource")) Then .VideoSource = SQLreader("VideoSource").ToString
                        If Not DBNull.Value.Equals(SQLreader("TMDB")) Then .TMDB = Convert.ToInt32(SQLreader("TMDB"))
                        If Not DBNull.Value.Equals(SQLreader("TMDBColID")) Then .TMDBColID = Convert.ToInt32(SQLreader("TMDBColID"))
                        If Not DBNull.Value.Equals(SQLreader("LastPlayed")) Then .LastPlayed = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(SQLreader("LastPlayed"))).ToString("yyyy-MM-dd HH:mm:ss")
                        If Not DBNull.Value.Equals(SQLreader("Language")) Then .Language = SQLreader("Language").ToString
                        If Not DBNull.Value.Equals(SQLreader("UserRating")) Then .UserRating = Convert.ToInt32(SQLreader("UserRating"))
                    End With
                End If
            End Using
        End Using

        'Actors
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT A.strRole, B.idActor, B.strActor, B.strThumb, C.url FROM actorlinkmovie AS A ",
                        "INNER JOIN actors AS B ON (A.idActor = B.idActor) ",
                        "LEFT OUTER JOIN art AS C ON (B.idActor = C.media_id AND C.media_type = 'actor' AND C.type = 'thumb') ",
                        "WHERE A.idMovie = ", _movieDB.ID, " ",
                        "ORDER BY A.iOrder;")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim person As MediaContainers.Person
                While SQLreader.Read
                    person = New MediaContainers.Person
                    person.ID = Convert.ToInt64(SQLreader("idActor"))
                    person.Name = SQLreader("strActor").ToString
                    person.Role = SQLreader("strRole").ToString
                    person.LocalFilePath = SQLreader("url").ToString
                    person.URLOriginal = SQLreader("strThumb").ToString
                    _movieDB.MainDetails.Actors.Add(person)
                End While
            End Using
        End Using

        'Countries
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.strCountry FROM countrylinkmovie ",
                                                   "AS A INNER JOIN country AS B ON (A.idCountry = B.idCountry) WHERE A.idMovie = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strCountry")) Then _movieDB.MainDetails.Countries.Add(SQLreader("strCountry").ToString)
                End While
            End Using
        End Using

        'Credits
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.strActor FROM writerlinkmovie ",
                                                   "AS A INNER JOIN actors AS B ON (A.idWriter = B.idActor) WHERE A.idMovie = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strActor")) Then _movieDB.MainDetails.Credits.Add(SQLreader("strActor").ToString)
                End While
            End Using
        End Using

        'Directors
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.strActor FROM directorlinkmovie ",
                                                   "AS A INNER JOIN actors AS B ON (A.idDirector = B.idActor) WHERE A.idMovie = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strActor")) Then _movieDB.MainDetails.Directors.Add(SQLreader("strActor").ToString)
                End While
            End Using
        End Using

        'Genres
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.strGenre FROM genrelinkmovie ",
                                                   "AS A INNER JOIN genre AS B ON (A.idGenre = B.idGenre) WHERE A.idMovie = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strGenre")) Then _movieDB.MainDetails.Genres.Add(SQLreader("strGenre").ToString)
                End While
            End Using
        End Using

        'Video streams
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM MoviesVStreams WHERE MovieID = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim video As MediaContainers.Video
                While SQLreader.Read
                    video = New MediaContainers.Video
                    If Not DBNull.Value.Equals(SQLreader("Video_Width")) Then video.Width = SQLreader("Video_Width").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Height")) Then video.Height = SQLreader("Video_Height").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Codec")) Then video.Codec = SQLreader("Video_Codec").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Duration")) Then video.Duration = SQLreader("Video_Duration").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_ScanType")) Then video.Scantype = SQLreader("Video_ScanType").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_AspectDisplayRatio")) Then video.Aspect = SQLreader("Video_AspectDisplayRatio").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Language")) Then video.Language = SQLreader("Video_Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_LongLanguage")) Then video.LongLanguage = SQLreader("Video_LongLanguage").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Bitrate")) Then video.Bitrate = SQLreader("Video_Bitrate").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_MultiViewCount")) Then video.MultiViewCount = SQLreader("Video_MultiViewCount").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_FileSize")) Then video.Filesize = Convert.ToInt64(SQLreader("Video_FileSize"))
                    If Not DBNull.Value.Equals(SQLreader("Video_MultiViewLayout")) Then video.MultiViewLayout = SQLreader("Video_MultiViewLayout").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_StereoMode")) Then video.StereoMode = SQLreader("Video_StereoMode").ToString
                    _movieDB.MainDetails.FileInfo.StreamDetails.Video.Add(video)
                End While
            End Using
        End Using

        'Audio streams
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM MoviesAStreams WHERE MovieID = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim audio As MediaContainers.Audio
                While SQLreader.Read
                    audio = New MediaContainers.Audio
                    If Not DBNull.Value.Equals(SQLreader("Audio_Language")) Then audio.Language = SQLreader("Audio_Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("Audio_LongLanguage")) Then audio.LongLanguage = SQLreader("Audio_LongLanguage").ToString
                    If Not DBNull.Value.Equals(SQLreader("Audio_Codec")) Then audio.Codec = SQLreader("Audio_Codec").ToString
                    If Not DBNull.Value.Equals(SQLreader("Audio_Channel")) Then audio.Channels = SQLreader("Audio_Channel").ToString
                    If Not DBNull.Value.Equals(SQLreader("Audio_Bitrate")) Then audio.Bitrate = SQLreader("Audio_Bitrate").ToString
                    _movieDB.MainDetails.FileInfo.StreamDetails.Audio.Add(audio)
                End While
            End Using
        End Using

        'embedded subtitles
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM MoviesSubs WHERE MovieID = ", _movieDB.ID, " AND NOT Subs_Type = 'External';")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim subtitle As MediaContainers.Subtitle
                While SQLreader.Read
                    subtitle = New MediaContainers.Subtitle
                    If Not DBNull.Value.Equals(SQLreader("Subs_Language")) Then subtitle.Language = SQLreader("Subs_Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_LongLanguage")) Then subtitle.LongLanguage = SQLreader("Subs_LongLanguage").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_Type")) Then subtitle.SubsType = SQLreader("Subs_Type").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_Path")) Then subtitle.SubsPath = SQLreader("Subs_Path").ToString
                    subtitle.SubsForced = Convert.ToBoolean(SQLreader("Subs_Forced"))
                    _movieDB.MainDetails.FileInfo.StreamDetails.Subtitle.Add(subtitle)
                End While
            End Using
        End Using

        'external subtitles
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM MoviesSubs WHERE MovieID = ", _movieDB.ID, " AND Subs_Type = 'External';")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim subtitle As MediaContainers.Subtitle
                While SQLreader.Read
                    subtitle = New MediaContainers.Subtitle
                    If Not DBNull.Value.Equals(SQLreader("Subs_Language")) Then subtitle.Language = SQLreader("Subs_Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_LongLanguage")) Then subtitle.LongLanguage = SQLreader("Subs_LongLanguage").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_Type")) Then subtitle.SubsType = SQLreader("Subs_Type").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_Path")) Then subtitle.SubsPath = SQLreader("Subs_Path").ToString
                    subtitle.SubsForced = Convert.ToBoolean(SQLreader("Subs_Forced"))
                    _movieDB.Subtitles.Add(subtitle)
                End While
            End Using
        End Using

        'MovieSets
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT A.idMovie, A.idSet, A.iOrder, B.idSet, B.Plot, B.SetName, B.TMDBColID FROM setlinkmovie ",
                                                   "AS A INNER JOIN sets AS B ON (A.idSet = B.idSet) WHERE A.idMovie = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Dim tSet As New MediaContainers.SetDetails
                    If Not DBNull.Value.Equals(SQLreader("idSet")) Then tSet.ID = Convert.ToInt64(SQLreader("idSet"))
                    If Not DBNull.Value.Equals(SQLreader("iOrder")) Then tSet.Order = CInt(SQLreader("iOrder"))
                    If Not DBNull.Value.Equals(SQLreader("Plot")) Then tSet.Plot = SQLreader("Plot").ToString
                    If Not DBNull.Value.Equals(SQLreader("SetName")) Then tSet.Title = SQLreader("SetName").ToString
                    If Not DBNull.Value.Equals(SQLreader("TMDBColID")) Then tSet.TMDB = Convert.ToInt32(SQLreader("TMDBColID"))
                    _movieDB.MainDetails.Sets.Add(tSet)
                End While
            End Using
        End Using

        'ShowLinks
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.Title FROM movielinktvshow ",
                                                   "AS A INNER JOIN tvshow AS B ON (A.idShow = B.idShow) WHERE A.idMovie = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("Title")) Then _movieDB.MainDetails.ShowLinks.Add(SQLreader("Title").ToString)
                End While
            End Using
        End Using

        'Studios
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.strStudio FROM studiolinkmovie ",
                                                   "AS A INNER JOIN studio AS B ON (A.idStudio = B.idStudio) WHERE A.idMovie = ", _movieDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strStudio")) Then _movieDB.MainDetails.Studios.Add(SQLreader("strStudio").ToString)
                End While
            End Using
        End Using

        'Tags
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.strTag FROM taglinks ",
                                                   "AS A INNER JOIN tag AS B ON (A.idTag = B.idTag) WHERE A.idMedia = ", _movieDB.ID, " AND A.media_type = 'movie';")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strTag")) Then _movieDB.MainDetails.Tags.Add(SQLreader("strTag").ToString)
                End While
            End Using
        End Using

        'ImagesContainer
        _movieDB.ImagesContainer.Banner.LocalFilePath = GetArtForItem(_movieDB.ID, "movie", "banner")
        _movieDB.ImagesContainer.ClearArt.LocalFilePath = GetArtForItem(_movieDB.ID, "movie", "clearart")
        _movieDB.ImagesContainer.ClearLogo.LocalFilePath = GetArtForItem(_movieDB.ID, "movie", "clearlogo")
        _movieDB.ImagesContainer.DiscArt.LocalFilePath = GetArtForItem(_movieDB.ID, "movie", "discart")
        _movieDB.ImagesContainer.Fanart.LocalFilePath = GetArtForItem(_movieDB.ID, "movie", "fanart")
        _movieDB.ImagesContainer.Landscape.LocalFilePath = GetArtForItem(_movieDB.ID, "movie", "landscape")
        _movieDB.ImagesContainer.Poster.LocalFilePath = GetArtForItem(_movieDB.ID, "movie", "poster")
        If Not String.IsNullOrEmpty(_movieDB.ExtrafanartsPath) AndAlso Directory.Exists(_movieDB.ExtrafanartsPath) Then
            For Each ePath As String In Directory.GetFiles(_movieDB.ExtrafanartsPath, "*.jpg")
                _movieDB.ImagesContainer.Extrafanarts.Add(New MediaContainers.Image With {.LocalFilePath = ePath})
            Next
        End If
        If Not String.IsNullOrEmpty(_movieDB.ExtrathumbsPath) AndAlso Directory.Exists(_movieDB.ExtrathumbsPath) Then
            Dim iIndex As Integer = 0
            For Each ePath As String In Directory.GetFiles(_movieDB.ExtrathumbsPath, "thumb*.jpg")
                _movieDB.ImagesContainer.Extrathumbs.Add(New MediaContainers.Image With {.Index = iIndex, .LocalFilePath = ePath})
                iIndex += 1
            Next
        End If

        'Check if the file is available and ready to edit
        If _movieDB.FileItemSpecified AndAlso File.Exists(_movieDB.FileItem.FirstStackedPath) Then _movieDB.IsOnline = True

        Return _movieDB
    End Function

    ''' <summary>
    ''' Load all the information for a movie (by movie path)
    ''' </summary>
    ''' <param name="strPath">Full path to the movie file</param>
    ''' <returns>Database.DBElement object</returns>
    Public Function Load_Movie(ByVal strPath As String) As DBElement
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            ' One more Query Better then re-write all function again
            SQLcommand.CommandText = String.Concat("SELECT idMovie FROM movie WHERE MoviePath = ", strPath, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.Read Then
                    Return Load_Movie(Convert.ToInt64(SQLreader("idMovie")))
                End If
            End Using
        End Using

        Return New DBElement(Enums.ContentType.Movie)
    End Function

    ''' <summary>
    ''' Load all the information for a movieset.
    ''' </summary>
    ''' <param name="lngMovieSetID">ID of the movieset to load, as stored in the database</param>
    ''' <returns>Database.DBElement object</returns>
    Public Function Load_MovieSet(ByVal lngMovieSetID As Long) As DBElement
        Dim _moviesetDB As New DBElement(Enums.ContentType.Movieset)
        _moviesetDB.MainDetails = New MediaContainers.MainDetails

        _moviesetDB.ID = lngMovieSetID
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM sets WHERE idSet = ", lngMovieSetID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.HasRows Then
                    SQLreader.Read()
                    If Not DBNull.Value.Equals(SQLreader("ListTitle")) Then _moviesetDB.ListTitle = SQLreader("ListTitle").ToString
                    If Not DBNull.Value.Equals(SQLreader("NfoPath")) Then _moviesetDB.NfoPath = SQLreader("NfoPath").ToString
                    If Not DBNull.Value.Equals(SQLreader("Language")) Then _moviesetDB.Language = SQLreader("Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("DateModified")) Then _moviesetDB.DateModified = Convert.ToInt64(SQLreader("DateModified"))

                    _moviesetDB.IsMark = Convert.ToBoolean(SQLreader("Mark"))
                    _moviesetDB.IsLock = Convert.ToBoolean(SQLreader("Lock"))
                    _moviesetDB.SortMethod = DirectCast(Convert.ToInt32(SQLreader("SortMethod")), Enums.SortMethod_MovieSet)

                    With _moviesetDB.MainDetails
                        If Not DBNull.Value.Equals(SQLreader("TMDBColID")) Then .TMDB = Convert.ToInt32(SQLreader("TMDBColID"))
                        If Not DBNull.Value.Equals(SQLreader("Plot")) Then .Plot = SQLreader("Plot").ToString
                        If Not DBNull.Value.Equals(SQLreader("SetName")) Then .Title = SQLreader("SetName").ToString
                        If Not DBNull.Value.Equals(SQLreader("Language")) Then .Language = SQLreader("Language").ToString
                        If Not DBNull.Value.Equals(SQLreader("DateModified")) Then .DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(SQLreader("DateModified"))).ToString("yyyy-MM-dd HH:mm:ss")
                        .OldTitle = .Title
                    End With
                End If
            End Using
        End Using

        'Movies in Set
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            If Not Master.eSettings.Movie.DataSettings.CollectionYAMJCompatible Then
                If _moviesetDB.SortMethod = Enums.SortMethod_MovieSet.Year Then
                    SQLcommand.CommandText = String.Concat("SELECT setlinkmovie.idMovie, setlinkmovie.iOrder FROM setlinkmovie INNER JOIN movie ON (setlinkmovie.idMovie = movie.idMovie) ",
                                                           "WHERE idSet = ", _moviesetDB.ID, " ORDER BY movie.Year;")
                ElseIf _moviesetDB.SortMethod = Enums.SortMethod_MovieSet.Title Then
                    SQLcommand.CommandText = String.Concat("SELECT setlinkmovie.idMovie, setlinkmovie.iOrder FROM setlinkmovie INNER JOIN movielist ON (setlinkmovie.idMovie = movielist.idMovie) ",
                                                           "WHERE idSet = ", _moviesetDB.ID, " ORDER BY movielist.SortedTitle COLLATE NOCASE;")
                End If
            Else
                SQLcommand.CommandText = String.Concat("SELECT setlinkmovie.idMovie, setlinkmovie.iOrder FROM setlinkmovie ",
                                                       "WHERE idSet = ", _moviesetDB.ID, " ORDER BY iOrder;")
            End If
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim i As Integer = 0
                While SQLreader.Read
                    _moviesetDB.MoviesInSet.Add(New MediaContainers.MovieInSet With {
                                                .DBMovie = Load_Movie(Convert.ToInt64(SQLreader("idMovie"))),
                                                .Order = i})
                    i += 1
                End While
            End Using
        End Using

        'ImagesContainer
        _moviesetDB.ImagesContainer.Banner.LocalFilePath = GetArtForItem(_moviesetDB.ID, "set", "banner")
        _moviesetDB.ImagesContainer.ClearArt.LocalFilePath = GetArtForItem(_moviesetDB.ID, "set", "clearart")
        _moviesetDB.ImagesContainer.ClearLogo.LocalFilePath = GetArtForItem(_moviesetDB.ID, "set", "clearlogo")
        _moviesetDB.ImagesContainer.DiscArt.LocalFilePath = GetArtForItem(_moviesetDB.ID, "set", "discart")
        _moviesetDB.ImagesContainer.Fanart.LocalFilePath = GetArtForItem(_moviesetDB.ID, "set", "fanart")
        _moviesetDB.ImagesContainer.Landscape.LocalFilePath = GetArtForItem(_moviesetDB.ID, "set", "landscape")
        _moviesetDB.ImagesContainer.Poster.LocalFilePath = GetArtForItem(_moviesetDB.ID, "set", "poster")

        Return _moviesetDB
    End Function

    Public Function Load_Source_Movie(ByVal lngSourceID As Long) As DBSource
        Dim _source As New DBSource

        _source.ID = lngSourceID
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM moviesource WHERE idSource = ", _source.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.HasRows Then
                    SQLreader.Read()
                    _source.Name = SQLreader("strName").ToString
                    _source.Path = SQLreader("strPath").ToString
                    _source.Recursive = Convert.ToBoolean(SQLreader("bRecursive"))
                    _source.UseFolderName = Convert.ToBoolean(SQLreader("bFoldername"))
                    _source.IsSingle = Convert.ToBoolean(SQLreader("bSingle"))
                    _source.Exclude = Convert.ToBoolean(SQLreader("bExclude"))
                    _source.GetYear = Convert.ToBoolean(SQLreader("bGetYear"))
                    _source.Language = SQLreader("strLanguage").ToString
                    _source.LastScan = SQLreader("strLastScan").ToString
                End If
            End Using
        End Using

        Return _source
    End Function
    ''' <summary>
    ''' Load Movie Sources from the DB. This populates the Master.MovieSources list of movie Sources
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub Load_Sources_Movie()
        Master.MovieSources.Clear()
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT * FROM moviesource;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Try ' Parsing database entry may fail. If it does, log the error and ignore the entry but continue processing
                        Dim msource As New DBSource
                        msource.ID = Convert.ToInt64(SQLreader("idSource"))
                        msource.Name = SQLreader("strName").ToString
                        msource.Path = SQLreader("strPath").ToString
                        msource.Recursive = Convert.ToBoolean(SQLreader("bRecursive"))
                        msource.UseFolderName = Convert.ToBoolean(SQLreader("bFoldername"))
                        msource.IsSingle = Convert.ToBoolean(SQLreader("bSingle"))
                        msource.Exclude = Convert.ToBoolean(SQLreader("bExclude"))
                        msource.GetYear = Convert.ToBoolean(SQLreader("bGetYear"))
                        msource.Language = SQLreader("strLanguage").ToString
                        msource.LastScan = SQLreader("strLastScan").ToString
                        Master.MovieSources.Add(msource)
                    Catch ex As Exception
                        logger.Error(ex, New StackFrame().GetMethod().Name)
                    End Try
                End While
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' Load all the information for a movietag.
    ''' </summary>
    ''' <param name="lngTagID">ID of the movietag to load, as stored in the database</param>
    ''' <returns>Database.DBElementTag object</returns>
    Public Function Load_Tag_Movie(ByVal lngTagID As Integer) As Structures.DBMovieTag
        Dim _tagDB As New Structures.DBMovieTag
        _tagDB.ID = lngTagID
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM tag WHERE idTag = ", lngTagID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.HasRows Then
                    SQLreader.Read()
                    If Not DBNull.Value.Equals(SQLreader("strTag")) Then _tagDB.Title = SQLreader("strTag").ToString
                    If Not DBNull.Value.Equals(SQLreader("idTag")) Then _tagDB.ID = CInt(SQLreader("idTag"))
                End If
            End Using
        End Using

        _tagDB.Movies = New List(Of DBElement)
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM taglinks ",
                        "WHERE idTag = ", _tagDB.ID, " AND media_type = 'movie';")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    _tagDB.Movies.Add(Load_Movie(Convert.ToInt64(SQLreader("idMedia"))))
                End While
            End Using
        End Using
        Return _tagDB
    End Function

    Public Function Load_AllTVEpisodes(ByVal lngShowID As Long, ByVal bWithShow As Boolean, Optional ByVal intOnlySeason As Integer = -1, Optional ByVal bWithMissingEpisodes As Boolean = False) As List(Of DBElement)
        If lngShowID < 0 Then Throw New ArgumentOutOfRangeException("ShowID", "Value must be >= 0, was given: " & lngShowID)

        Dim _TVEpisodesList As New List(Of DBElement)

        Using SQLCount As SQLiteCommand = Master.DB.MyVideosDBConn.CreateCommand()
            If intOnlySeason = -1 Then
                SQLCount.CommandText = String.Concat("SELECT COUNT(idEpisode) AS eCount FROM episode WHERE idShow = ", lngShowID, If(bWithMissingEpisodes, ";", " AND NOT idFile = -1;"))
            Else
                SQLCount.CommandText = String.Concat("SELECT COUNT(idEpisode) AS eCount FROM episode WHERE idShow = ", lngShowID, " AND Season = ", intOnlySeason, If(bWithMissingEpisodes, ";", " AND NOT idFile = -1;"))
            End If
            Using SQLRCount As SQLiteDataReader = SQLCount.ExecuteReader
                If SQLRCount.HasRows Then
                    SQLRCount.Read()
                    If Convert.ToInt32(SQLRCount("eCount")) > 0 Then
                        Using SQLCommand As SQLiteCommand = Master.DB.MyVideosDBConn.CreateCommand()
                            If intOnlySeason = -1 Then
                                SQLCommand.CommandText = String.Concat("SELECT * FROM episode WHERE idShow = ", lngShowID, If(bWithMissingEpisodes, ";", " AND NOT idFile = -1;"))
                            Else
                                SQLCommand.CommandText = String.Concat("SELECT * FROM episode WHERE idShow = ", lngShowID, " AND Season = ", intOnlySeason, If(bWithMissingEpisodes, ";", " AND NOT idFile = -1;"))
                            End If
                            Using SQLReader As SQLiteDataReader = SQLCommand.ExecuteReader
                                While SQLReader.Read
                                    _TVEpisodesList.Add(Master.DB.Load_TVEpisode(Convert.ToInt64(SQLReader("idEpisode")), bWithShow))
                                End While
                            End Using
                        End Using
                    End If
                End If
            End Using
        End Using

        Return _TVEpisodesList
    End Function

    Public Function Load_AllTVEpisodes_ByFileID(ByVal lngFileID As Long, ByVal bWithShow As Boolean) As List(Of DBElement)
        If lngFileID < 0 Then Throw New ArgumentOutOfRangeException("idFile", "Value must be >= 0, was given: " & lngFileID)

        Dim _TVEpisodesList As New List(Of DBElement)

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT idEpisode FROM episode WHERE idFile = {0};", lngFileID)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.HasRows Then
                    While SQLreader.Read()
                        _TVEpisodesList.Add(Master.DB.Load_TVEpisode(Convert.ToInt64(SQLreader("idEpisode")), bWithShow))
                    End While
                End If
            End Using
        End Using

        Return _TVEpisodesList
    End Function

    Public Function Load_AllTVSeasons(ByVal lngShowID As Long) As List(Of DBElement)
        If lngShowID < 0 Then Throw New ArgumentOutOfRangeException("ShowID", "Value must be >= 0, was given: " & lngShowID)

        Dim _TVSeasonsList As New List(Of DBElement)

        Using SQLCount As SQLiteCommand = Master.DB.MyVideosDBConn.CreateCommand()
            SQLCount.CommandText = String.Concat("SELECT COUNT(idSeason) AS eCount FROM seasons WHERE idShow = ", lngShowID, ";")
            Using SQLRCount As SQLiteDataReader = SQLCount.ExecuteReader
                If SQLRCount.HasRows Then
                    SQLRCount.Read()
                    If Convert.ToInt32(SQLRCount("eCount")) > 0 Then
                        Using SQLCommand As SQLiteCommand = Master.DB.MyVideosDBConn.CreateCommand()
                            SQLCommand.CommandText = String.Concat("SELECT * FROM seasons WHERE idShow = ", lngShowID, ";")
                            Using SQLReader As SQLiteDataReader = SQLCommand.ExecuteReader
                                While SQLReader.Read
                                    _TVSeasonsList.Add(Master.DB.Load_TVSeason(Convert.ToInt64(SQLReader("idSeason")), False, False))
                                End While
                            End Using
                        End Using
                    End If
                End If
            End Using
        End Using

        Return _TVSeasonsList
    End Function
    ''' <summary>
    ''' Load all the information for a TV Season by ShowID and Season #.
    ''' </summary>
    ''' <param name="lngShowID">ID of the show to load, as stored in the database</param>
    ''' <returns>MediaContainers.SeasonDetails object</returns>
    ''' <remarks></remarks>
    Public Function Load_AllTVSeasonsDetails(ByVal lngShowID As Long) As MediaContainers.Seasons
        If lngShowID < 0 Then Throw New ArgumentOutOfRangeException("ShowID", "Value must be >= 0, was given: " & lngShowID)

        Dim _SeasonList As New MediaContainers.Seasons

        Using SQLcommandTVSeason As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommandTVSeason.CommandText = String.Concat("SELECT * FROM seasons WHERE idShow = ", lngShowID, " ORDER BY Season;")
            Using SQLReader As SQLiteDataReader = SQLcommandTVSeason.ExecuteReader
                While SQLReader.Read
                    Dim nSeason As New MediaContainers.MainDetails
                    If Not DBNull.Value.Equals(SQLReader("Aired")) Then nSeason.Aired = CStr(SQLReader("Aired"))
                    If Not DBNull.Value.Equals(SQLReader("Plot")) Then nSeason.Plot = CStr(SQLReader("Plot"))
                    If Not DBNull.Value.Equals(SQLReader("Season")) Then nSeason.Season = CInt(SQLReader("Season"))
                    If Not DBNull.Value.Equals(SQLReader("SeasonText")) Then nSeason.Title = CStr(SQLReader("SeasonText"))
                    If Not DBNull.Value.Equals(SQLReader("TMDB")) Then nSeason.TMDB = CInt(SQLReader("TMDB"))
                    If Not DBNull.Value.Equals(SQLReader("TVDB")) Then nSeason.TVDB = CInt(SQLReader("TVDB"))
                    _SeasonList.Seasons.Add(nSeason)
                End While
            End Using
        End Using

        Return _SeasonList
    End Function
    ''' <summary>
    ''' Load all the information for a TV Episode
    ''' </summary>
    ''' <param name="lngEpisodeID">Episode ID</param>
    ''' <param name="bWithShow">>If <c>True</c>, also retrieve the TV Show information</param>
    ''' <returns>Database.DBElement object</returns>
    Public Function Load_TVEpisode(ByVal lngEpisodeID As Long, ByVal bWithShow As Boolean) As DBElement
        Dim _TVDB As New DBElement(Enums.ContentType.TVEpisode)
        _TVDB.MainDetails = New MediaContainers.MainDetails
        Dim PathID As Long = -1

        _TVDB.ID = lngEpisodeID
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM episode WHERE idEpisode = ", lngEpisodeID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.HasRows Then
                    SQLreader.Read()
                    If Not DBNull.Value.Equals(SQLreader("NfoPath")) Then _TVDB.NfoPath = SQLreader("NfoPath").ToString
                    If Not DBNull.Value.Equals(SQLreader("idShow")) Then _TVDB.ShowID = Convert.ToInt64(SQLreader("idShow"))
                    If Not DBNull.Value.Equals(SQLreader("DateAdded")) Then _TVDB.DateAdded = Convert.ToInt64(SQLreader("DateAdded"))
                    If Not DBNull.Value.Equals(SQLreader("DateModified")) Then _TVDB.DateModified = Convert.ToInt64(SQLreader("DateModified"))
                    If Not DBNull.Value.Equals(SQLreader("VideoSource")) Then _TVDB.VideoSource = SQLreader("VideoSource").ToString
                    PathID = Convert.ToInt64(SQLreader("idFile"))

                    _TVDB.Source = Load_Source_TVShow(Convert.ToInt64(SQLreader("idSource")))

                    _TVDB.FileID = PathID
                    _TVDB.IsMark = Convert.ToBoolean(SQLreader("Mark"))
                    _TVDB.IsLock = Convert.ToBoolean(SQLreader("Lock"))
                    _TVDB.ShowID = Convert.ToInt64(SQLreader("idShow"))
                    _TVDB.ShowPath = Load_Path_TVShow(Convert.ToInt64(SQLreader("idShow")))

                    With _TVDB.MainDetails
                        If Not DBNull.Value.Equals(SQLreader("Title")) Then .Title = SQLreader("Title").ToString
                        If Not DBNull.Value.Equals(SQLreader("Season")) Then .Season = Convert.ToInt32(SQLreader("Season"))
                        If Not DBNull.Value.Equals(SQLreader("Episode")) Then .Episode = Convert.ToInt32(SQLreader("Episode"))
                        If Not DBNull.Value.Equals(SQLreader("DisplaySeason")) Then .DisplaySeason = Convert.ToInt32(SQLreader("DisplaySeason"))
                        If Not DBNull.Value.Equals(SQLreader("DisplayEpisode")) Then .DisplayEpisode = Convert.ToInt32(SQLreader("DisplayEpisode"))
                        If Not DBNull.Value.Equals(SQLreader("Aired")) Then .Aired = SQLreader("Aired").ToString
                        If Not DBNull.Value.Equals(SQLreader("Rating")) Then .Rating = SQLreader("Rating").ToString
                        If Not DBNull.Value.Equals(SQLreader("Plot")) Then .Plot = SQLreader("Plot").ToString
                        If Not DBNull.Value.Equals(SQLreader("Playcount")) Then .PlayCount = Convert.ToInt32(SQLreader("Playcount"))
                        If Not DBNull.Value.Equals(SQLreader("DateAdded")) Then .DateAdded = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(SQLreader("DateAdded"))).ToString("yyyy-MM-dd HH:mm:ss")
                        If Not DBNull.Value.Equals(SQLreader("DateModified")) Then .DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(SQLreader("DateModified"))).ToString("yyyy-MM-dd HH:mm:ss")
                        If Not DBNull.Value.Equals(SQLreader("Runtime")) Then .Runtime = SQLreader("Runtime").ToString
                        If Not DBNull.Value.Equals(SQLreader("Votes")) Then .Votes = SQLreader("Votes").ToString
                        If Not DBNull.Value.Equals(SQLreader("VideoSource")) Then .VideoSource = SQLreader("VideoSource").ToString
                        If Not DBNull.Value.Equals(SQLreader("SubEpisode")) Then .SubEpisode = Convert.ToInt32(SQLreader("SubEpisode"))
                        If Not DBNull.Value.Equals(SQLreader("LastPlayed")) Then .LastPlayed = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(SQLreader("LastPlayed"))).ToString("yyyy-MM-dd HH:mm:ss")
                        If Not DBNull.Value.Equals(SQLreader("IMDB")) Then .IMDB = SQLreader("IMDB").ToString
                        If Not DBNull.Value.Equals(SQLreader("TMDB")) Then .TMDB = Convert.ToInt32(SQLreader("TMDB"))
                        If Not DBNull.Value.Equals(SQLreader("TVDB")) Then .TVDB = Convert.ToInt32(SQLreader("TVDB"))
                        If Not DBNull.Value.Equals(SQLreader("UserRating")) Then .UserRating = Convert.ToInt32(SQLreader("UserRating"))
                    End With
                End If
            End Using
        End Using

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT strFilename FROM files WHERE idFile = ", PathID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader
                If SQLreader.HasRows Then
                    SQLreader.Read()
                    If Not DBNull.Value.Equals(SQLreader("strFilename")) Then
                        _TVDB.FileItem = New FileItem(SQLreader("strFilename").ToString)
                    End If
                End If
            End Using
        End Using

        'Actors
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT A.strRole, B.idActor, B.strActor, B.strThumb, C.url FROM actorlinkepisode AS A ",
                                                   "INNER JOIN actors AS B ON (A.idActor = B.idActor) ",
                                                   "LEFT OUTER JOIN art AS C ON (B.idActor = C.media_id AND C.media_type = 'actor' AND C.type = 'thumb') ",
                                                   "WHERE A.idEpisode = ", _TVDB.ID, " ",
                                                   "ORDER BY A.iOrder;")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim person As MediaContainers.Person
                While SQLreader.Read
                    person = New MediaContainers.Person
                    person.ID = Convert.ToInt64(SQLreader("idActor"))
                    person.Name = SQLreader("strActor").ToString
                    person.Role = SQLreader("strRole").ToString
                    person.LocalFilePath = SQLreader("url").ToString
                    person.URLOriginal = SQLreader("strThumb").ToString
                    _TVDB.MainDetails.Actors.Add(person)
                End While
            End Using
        End Using

        'Credits
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.strActor FROM writerlinkepisode ",
                                                   "AS A INNER JOIN actors AS B ON (A.idWriter = B.idActor) WHERE A.idEpisode = ", _TVDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strActor")) Then _TVDB.MainDetails.Credits.Add(SQLreader("strActor").ToString)
                End While
            End Using
        End Using

        'Directors
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.strActor FROM directorlinkepisode ",
                                                   "AS A INNER JOIN actors AS B ON (A.idDirector = B.idActor) WHERE A.idEpisode = ", _TVDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strActor")) Then _TVDB.MainDetails.Directors.Add(SQLreader("strActor").ToString)
                End While
            End Using
        End Using

        'Guest Stars
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT A.strRole, B.idActor, B.strActor, B.strThumb, C.url FROM gueststarlinkepisode AS A ",
                                                   "INNER JOIN actors AS B ON (A.idActor = B.idActor) ",
                                                   "LEFT OUTER JOIN art AS C ON (B.idActor = C.media_id AND C.media_type = 'actor' AND C.type = 'thumb') ",
                                                   "WHERE A.idEpisode = ", _TVDB.ID, " ",
                                                   "ORDER BY A.iOrder;")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim person As MediaContainers.Person
                While SQLreader.Read
                    person = New MediaContainers.Person
                    person.ID = Convert.ToInt64(SQLreader("idActor"))
                    person.Name = SQLreader("strActor").ToString
                    person.Role = SQLreader("strRole").ToString
                    person.LocalFilePath = SQLreader("url").ToString
                    person.URLOriginal = SQLreader("strThumb").ToString
                    _TVDB.MainDetails.Actors.Add(person)
                End While
            End Using
        End Using

        'Video Streams
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM TVVStreams WHERE TVEpID = ", _TVDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim video As MediaContainers.Video
                While SQLreader.Read
                    video = New MediaContainers.Video
                    If Not DBNull.Value.Equals(SQLreader("Video_Width")) Then video.Width = SQLreader("Video_Width").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Height")) Then video.Height = SQLreader("Video_Height").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Codec")) Then video.Codec = SQLreader("Video_Codec").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Duration")) Then video.Duration = SQLreader("Video_Duration").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_ScanType")) Then video.Scantype = SQLreader("Video_ScanType").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_AspectDisplayRatio")) Then video.Aspect = SQLreader("Video_AspectDisplayRatio").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Language")) Then video.Language = SQLreader("Video_Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_LongLanguage")) Then video.LongLanguage = SQLreader("Video_LongLanguage").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_Bitrate")) Then video.Bitrate = SQLreader("Video_Bitrate").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_MultiViewCount")) Then video.MultiViewCount = SQLreader("Video_MultiViewCount").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_FileSize")) Then video.Filesize = Convert.ToInt64(SQLreader("Video_FileSize"))
                    If Not DBNull.Value.Equals(SQLreader("Video_MultiViewLayout")) Then video.MultiViewLayout = SQLreader("Video_MultiViewLayout").ToString
                    If Not DBNull.Value.Equals(SQLreader("Video_StereoMode")) Then video.StereoMode = SQLreader("Video_StereoMode").ToString
                    _TVDB.MainDetails.FileInfo.StreamDetails.Video.Add(video)
                End While
            End Using
        End Using

        'Audio Streams
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM TVAStreams WHERE TVEpID = ", _TVDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim audio As MediaContainers.Audio
                While SQLreader.Read
                    audio = New MediaContainers.Audio
                    If Not DBNull.Value.Equals(SQLreader("Audio_Language")) Then audio.Language = SQLreader("Audio_Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("Audio_LongLanguage")) Then audio.LongLanguage = SQLreader("Audio_LongLanguage").ToString
                    If Not DBNull.Value.Equals(SQLreader("Audio_Codec")) Then audio.Codec = SQLreader("Audio_Codec").ToString
                    If Not DBNull.Value.Equals(SQLreader("Audio_Channel")) Then audio.Channels = SQLreader("Audio_Channel").ToString
                    If Not DBNull.Value.Equals(SQLreader("Audio_Bitrate")) Then audio.Bitrate = SQLreader("Audio_Bitrate").ToString
                    _TVDB.MainDetails.FileInfo.StreamDetails.Audio.Add(audio)
                End While
            End Using
        End Using

        'embedded subtitles
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM TVSubs WHERE TVEpID = ", _TVDB.ID, " AND NOT Subs_Type = 'External';")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim subtitle As MediaContainers.Subtitle
                While SQLreader.Read
                    subtitle = New MediaContainers.Subtitle
                    If Not DBNull.Value.Equals(SQLreader("Subs_Language")) Then subtitle.Language = SQLreader("Subs_Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_LongLanguage")) Then subtitle.LongLanguage = SQLreader("Subs_LongLanguage").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_Type")) Then subtitle.SubsType = SQLreader("Subs_Type").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_Path")) Then subtitle.SubsPath = SQLreader("Subs_Path").ToString
                    subtitle.SubsForced = Convert.ToBoolean(SQLreader("Subs_Forced"))
                    _TVDB.MainDetails.FileInfo.StreamDetails.Subtitle.Add(subtitle)
                End While
            End Using
        End Using

        'external subtitles
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM TVSubs WHERE TVEpID = ", _TVDB.ID, " AND Subs_Type = 'External';")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim subtitle As MediaContainers.Subtitle
                While SQLreader.Read
                    subtitle = New MediaContainers.Subtitle
                    If Not DBNull.Value.Equals(SQLreader("Subs_Language")) Then subtitle.Language = SQLreader("Subs_Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_LongLanguage")) Then subtitle.LongLanguage = SQLreader("Subs_LongLanguage").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_Type")) Then subtitle.SubsType = SQLreader("Subs_Type").ToString
                    If Not DBNull.Value.Equals(SQLreader("Subs_Path")) Then subtitle.SubsPath = SQLreader("Subs_Path").ToString
                    subtitle.SubsForced = Convert.ToBoolean(SQLreader("Subs_Forced"))
                    _TVDB.Subtitles.Add(subtitle)
                End While
            End Using
        End Using

        'ImagesContainer
        _TVDB.ImagesContainer.Fanart.LocalFilePath = GetArtForItem(_TVDB.ID, "episode", "fanart")
        _TVDB.ImagesContainer.Poster.LocalFilePath = GetArtForItem(_TVDB.ID, "episode", "thumb")

        'Show container
        If bWithShow Then
            _TVDB = Master.DB.AddTVShowInfoToDBElement(_TVDB)
        End If

        'Check if the file is available and ready to edit
        If _TVDB.FileItemSpecified AndAlso File.Exists(_TVDB.FileItem.FirstStackedPath) Then _TVDB.IsOnline = True

        Return _TVDB
    End Function
    ''' <summary>
    ''' Load all the information for a TV Show
    ''' </summary>
    ''' <param name="lngShowID">Show ID</param>
    ''' <returns>Database.DBElement object</returns>
    ''' <remarks></remarks>
    Public Function Load_TVShow_Full(ByVal lngShowID As Long) As DBElement
        If lngShowID < 0 Then Throw New ArgumentOutOfRangeException("ShowID", "Value must be >= 0, was given: " & lngShowID)
        Return Master.DB.Load_TVShow(lngShowID, True, True, True)
    End Function
    ''' <summary>
    ''' Load all the information for a TV Season
    ''' </summary>
    ''' <param name="lngSeasonID">Season ID</param>
    ''' <param name="bWithShow">If <c>True</c>, also retrieve the TV Show information</param>
    ''' <returns>Database.DBElement object</returns>
    ''' <remarks></remarks>
    Public Function Load_TVSeason(ByVal lngSeasonID As Long, ByVal bWithShow As Boolean, ByVal bWithEpisodes As Boolean) As DBElement
        Dim _TVDB As New DBElement(Enums.ContentType.TVSeason)
        _TVDB.MainDetails = New MediaContainers.MainDetails

        _TVDB.ID = lngSeasonID
        Using SQLcommandTVSeason As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommandTVSeason.CommandText = String.Concat("SELECT * FROM seasons WHERE idSeason = ", _TVDB.ID, ";")
            Using SQLReader As SQLiteDataReader = SQLcommandTVSeason.ExecuteReader
                If SQLReader.HasRows Then
                    SQLReader.Read()
                    _TVDB.IsLock = CBool(SQLReader("Lock"))
                    _TVDB.IsMark = CBool(SQLReader("Mark"))
                    _TVDB.ShowID = Convert.ToInt64(SQLReader("idShow"))
                    _TVDB.ShowPath = Load_Path_TVShow(Convert.ToInt64(SQLReader("idShow")))
                    If Not DBNull.Value.Equals(SQLReader("DateModified")) Then _TVDB.DateModified = Convert.ToInt64(SQLReader("DateModified"))

                    With _TVDB.MainDetails
                            If Not DBNull.Value.Equals(SQLReader("Aired")) Then .Aired = CStr(SQLReader("Aired"))
                            If Not DBNull.Value.Equals(SQLReader("Plot")) Then .Plot = CStr(SQLReader("Plot"))
                            If Not DBNull.Value.Equals(SQLReader("Season")) Then .Season = CInt(SQLReader("Season"))
                            If Not DBNull.Value.Equals(SQLReader("TMDB")) Then .TMDB = CInt(SQLReader("TMDB"))
                            If Not DBNull.Value.Equals(SQLReader("TVDB")) Then .TVDB = CInt(SQLReader("TVDB"))
                            If Not DBNull.Value.Equals(SQLReader("SeasonText")) Then .Title = CStr(SQLReader("SeasonText"))
                            If Not DBNull.Value.Equals(SQLReader("DateModified")) Then .DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(SQLReader("DateModified"))).ToString("yyyy-MM-dd HH:mm:ss")
                        End With
                    End If
            End Using
        End Using

        'ImagesContainer
        _TVDB.ImagesContainer.Banner.LocalFilePath = GetArtForItem(_TVDB.ID, "season", "banner")
        _TVDB.ImagesContainer.Fanart.LocalFilePath = GetArtForItem(_TVDB.ID, "season", "fanart")
        _TVDB.ImagesContainer.Landscape.LocalFilePath = GetArtForItem(_TVDB.ID, "season", "landscape")
        _TVDB.ImagesContainer.Poster.LocalFilePath = GetArtForItem(_TVDB.ID, "season", "poster")

        'Episodes
        If bWithEpisodes Then
            For Each tEpisode As DBElement In Load_AllTVEpisodes(_TVDB.ShowID, bWithShow, _TVDB.MainDetails.Season)
                tEpisode = AddTVShowInfoToDBElement(tEpisode, _TVDB)
                _TVDB.Episodes.Add(tEpisode)
            Next
        End If

        'Show container
        If bWithShow Then
            _TVDB = Master.DB.AddTVShowInfoToDBElement(_TVDB)
        End If

        Return _TVDB
    End Function
    ''' <summary>
    ''' Load all the information for a TV Show
    ''' </summary>
    ''' <param name="lngShowID">Show ID</param>
    ''' <param name="intSeason">Season number</param>
    ''' <param name="bWithShow">If <c>True</c>, also retrieve the TV Show information</param>
    ''' <returns>Database.DBElement object</returns>
    ''' <remarks></remarks>
    Public Function Load_TVSeason(ByVal lngShowID As Long, ByVal intSeason As Integer, ByVal bWithShow As Boolean, ByVal bWithEpisodes As Boolean) As DBElement
        Dim _TVDB As New DBElement(Enums.ContentType.TVSeason)

        If lngShowID < 0 Then Throw New ArgumentOutOfRangeException("ShowID", "Value must be >= 0, was given: " & lngShowID)

        _TVDB.ShowID = lngShowID
        If bWithShow Then AddTVShowInfoToDBElement(_TVDB)

        Using SQLcommandTVSeason As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommandTVSeason.CommandText = String.Concat("SELECT idSeason FROM seasons WHERE idShow = ", lngShowID, " AND Season = ", intSeason, ";")
            Using SQLReader As SQLiteDataReader = SQLcommandTVSeason.ExecuteReader
                If SQLReader.HasRows Then
                    SQLReader.Read()
                    _TVDB = Load_TVSeason(CInt(SQLReader("idSeason")), bWithShow, bWithEpisodes)
                End If
            End Using
        End Using

        Return _TVDB
    End Function
    ''' <summary>
    ''' Load all the information for a TV Show
    ''' </summary>
    ''' <param name="lngShowID">Show ID</param>
    ''' <returns>Database.DBElement object</returns>
    Public Function Load_TVShow(ByVal lngShowID As Long, ByVal bWithSeasons As Boolean, ByVal bWithEpisodes As Boolean, Optional ByVal bWithMissingEpisodes As Boolean = False) As DBElement
        Dim _TVDB As New DBElement(Enums.ContentType.TVShow)
        _TVDB.MainDetails = New MediaContainers.MainDetails

        If lngShowID < 0 Then Throw New ArgumentOutOfRangeException("ShowID", "Value must be >= 0, was given: " & lngShowID)

        _TVDB.ID = lngShowID
        _TVDB.ShowID = lngShowID
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM tvshow WHERE idShow = ", lngShowID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.HasRows Then
                    SQLreader.Read()
                    If Not DBNull.Value.Equals(SQLreader("ListTitle")) Then _TVDB.ListTitle = SQLreader("ListTitle").ToString
                    If Not DBNull.Value.Equals(SQLreader("EFanartsPath")) Then _TVDB.ExtrafanartsPath = SQLreader("EFanartsPath").ToString
                    If Not DBNull.Value.Equals(SQLreader("Language")) Then _TVDB.Language = SQLreader("Language").ToString
                    If Not DBNull.Value.Equals(SQLreader("NfoPath")) Then _TVDB.NfoPath = SQLreader("NfoPath").ToString
                    If Not DBNull.Value.Equals(SQLreader("TVShowPath")) Then _TVDB.ShowPath = SQLreader("TVShowPath").ToString
                    If Not DBNull.Value.Equals(SQLreader("ThemePath")) Then _TVDB.Theme.LocalFilePath = SQLreader("ThemePath").ToString
                    If Not DBNull.Value.Equals(SQLreader("DateModified")) Then _TVDB.DateModified = Convert.ToInt64(SQLreader("DateModified"))

                    _TVDB.Source = Load_Source_TVShow(Convert.ToInt64(SQLreader("idSource")))

                    _TVDB.IsMark = Convert.ToBoolean(SQLreader("Mark"))
                    _TVDB.IsLock = Convert.ToBoolean(SQLreader("Lock"))
                    _TVDB.Ordering = DirectCast(Convert.ToInt32(SQLreader("Ordering")), Enums.EpisodeOrdering)
                    _TVDB.EpisodeSorting = DirectCast(Convert.ToInt32(SQLreader("EpisodeSorting")), Enums.EpisodeSorting)

                    With _TVDB.MainDetails
                        If Not DBNull.Value.Equals(SQLreader("Title")) Then .Title = SQLreader("Title").ToString
                        If Not DBNull.Value.Equals(SQLreader("TVDB")) Then .TVDB = Convert.ToInt32(SQLreader("TVDB"))
                        If Not DBNull.Value.Equals(SQLreader("EpisodeGuide")) Then .EpisodeGuide.URL = SQLreader("EpisodeGuide").ToString
                        If Not DBNull.Value.Equals(SQLreader("Plot")) Then .Plot = SQLreader("Plot").ToString
                        If Not DBNull.Value.Equals(SQLreader("Premiered")) Then .Premiered = SQLreader("Premiered").ToString
                        If Not DBNull.Value.Equals(SQLreader("MPAA")) Then .MPAA = SQLreader("MPAA").ToString
                        If Not DBNull.Value.Equals(SQLreader("Rating")) Then .Rating = SQLreader("Rating").ToString
                        If Not DBNull.Value.Equals(SQLreader("Status")) Then .Status = SQLreader("Status").ToString
                        If Not DBNull.Value.Equals(SQLreader("Runtime")) Then .Runtime = SQLreader("Runtime").ToString
                        If Not DBNull.Value.Equals(SQLreader("Votes")) Then .Votes = SQLreader("Votes").ToString
                        If Not DBNull.Value.Equals(SQLreader("SortTitle")) Then .SortTitle = SQLreader("SortTitle").ToString
                        If Not DBNull.Value.Equals(SQLreader("IMDB")) Then .IMDB = SQLreader("IMDB").ToString
                        If Not DBNull.Value.Equals(SQLreader("TMDB")) Then .TMDB = Convert.ToInt32(SQLreader("TMDB"))
                        If Not DBNull.Value.Equals(SQLreader("Language")) Then .Language = SQLreader("Language").ToString
                        If Not DBNull.Value.Equals(SQLreader("OriginalTitle")) Then .OriginalTitle = SQLreader("OriginalTitle").ToString
                        If Not DBNull.Value.Equals(SQLreader("UserRating")) Then .UserRating = Convert.ToInt32(SQLreader("UserRating"))
                        If Not DBNull.Value.Equals(SQLreader("DateModified")) Then .DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(SQLreader("DateModified"))).ToString("yyyy-MM-dd HH:mm:ss")
                    End With
                End If
            End Using
        End Using

        'Actors
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT A.strRole, B.idActor, B.strActor, B.strThumb, C.url FROM actorlinktvshow AS A ",
                                                   "INNER JOIN actors AS B ON (A.idActor = B.idActor) ",
                                                   "LEFT OUTER JOIN art AS C ON (B.idActor = C.media_id AND C.media_type = 'actor' AND C.type = 'thumb') ",
                                                   "WHERE A.idShow = ", _TVDB.ID, " ",
                                                   "ORDER BY A.iOrder;")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim actor As MediaContainers.Person
                While SQLreader.Read
                    actor = New MediaContainers.Person
                    actor.ID = Convert.ToInt64(SQLreader("idActor"))
                    actor.Name = SQLreader("strActor").ToString
                    actor.Role = SQLreader("strRole").ToString
                    actor.LocalFilePath = SQLreader("url").ToString
                    actor.URLOriginal = SQLreader("strThumb").ToString
                    _TVDB.MainDetails.Actors.Add(actor)
                End While
            End Using
        End Using

        'Countries
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT country.strCountry ",
                                                   "FROM country ",
                                                   "INNER JOIN countrylinktvshow ON (country.idCountry = countrylinktvshow.idCountry) ",
                                                   "WHERE countrylinktvshow.idShow = ", _TVDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    _TVDB.MainDetails.Countries.Add(SQLreader("strCountry").ToString)
                End While
            End Using
        End Using

        'Creators
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT actors.strActor ",
                                                   "FROM actors ",
                                                   "INNER JOIN creatorlinktvshow ON (actors.idActor = creatorlinktvshow.idActor) ",
                                                   "WHERE creatorlinktvshow.idShow = ", _TVDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    _TVDB.MainDetails.Creators.Add(SQLreader("strActor").ToString)
                End While
            End Using
        End Using

        'Directors
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT actors.strActor ",
                                                   "FROM actors ",
                                                   "INNER JOIN directorlinktvshow ON (actors.idActor = directorlinktvshow.idDirector) ",
                                                   "WHERE directorlinktvshow.idShow = ", _TVDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strActor")) Then _TVDB.MainDetails.Directors.Add(SQLreader("strActor").ToString)
                End While
            End Using
        End Using

        'Genres
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT genre.strGenre ",
                                                   "FROM genre ",
                                                   "INNER JOIN genrelinktvshow ON (genre.idGenre = genrelinktvshow.idGenre) ",
                                                   "WHERE genrelinktvshow.idShow = ", _TVDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strGenre")) Then _TVDB.MainDetails.Genres.Add(SQLreader("strGenre").ToString)
                End While
            End Using
        End Using

        'Studios
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT studio.strStudio ",
                                                   "FROM studio ",
                                                   "INNER JOIN studiolinktvshow ON (studio.idStudio = studiolinktvshow.idStudio) ",
                                                   "WHERE studiolinktvshow.idShow = ", _TVDB.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("strStudio")) Then _TVDB.MainDetails.Studios.Add(SQLreader("strStudio").ToString)
                End While
            End Using
        End Using

        'Tags
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT B.strTag FROM taglinks ",
                                                   "AS A INNER JOIN tag AS B ON (A.idTag = B.idTag) WHERE A.idMedia = ", _TVDB.ID, " And A.media_type = 'tvshow';")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                Dim tag As String
                While SQLreader.Read
                    tag = String.Empty
                    If Not DBNull.Value.Equals(SQLreader("strTag")) Then tag = SQLreader("strTag").ToString
                    _TVDB.MainDetails.Tags.Add(tag)
                End While
            End Using
        End Using

        'ImagesContainer
        _TVDB.ImagesContainer.Banner.LocalFilePath = GetArtForItem(_TVDB.ID, "tvshow", "banner")
        _TVDB.ImagesContainer.CharacterArt.LocalFilePath = GetArtForItem(_TVDB.ID, "tvshow", "characterart")
        _TVDB.ImagesContainer.ClearArt.LocalFilePath = GetArtForItem(_TVDB.ID, "tvshow", "clearart")
        _TVDB.ImagesContainer.ClearLogo.LocalFilePath = GetArtForItem(_TVDB.ID, "tvshow", "clearlogo")
        _TVDB.ImagesContainer.Fanart.LocalFilePath = GetArtForItem(_TVDB.ID, "tvshow", "fanart")
        _TVDB.ImagesContainer.Landscape.LocalFilePath = GetArtForItem(_TVDB.ID, "tvshow", "landscape")
        _TVDB.ImagesContainer.Poster.LocalFilePath = GetArtForItem(_TVDB.ID, "tvshow", "poster")
        If Not String.IsNullOrEmpty(_TVDB.ExtrafanartsPath) AndAlso Directory.Exists(_TVDB.ExtrafanartsPath) Then
            For Each ePath As String In Directory.GetFiles(_TVDB.ExtrafanartsPath, "*.jpg")
                _TVDB.ImagesContainer.Extrafanarts.Add(New MediaContainers.Image With {.LocalFilePath = ePath})
            Next
        End If

        'Seasons
        If bWithSeasons Then
            For Each tSeason As DBElement In Load_AllTVSeasons(_TVDB.ID)
                tSeason = AddTVShowInfoToDBElement(tSeason, _TVDB)
                _TVDB.Seasons.Add(tSeason)
                _TVDB.MainDetails.Seasons.Seasons.Add(tSeason.MainDetails)
            Next
            '_TVDB.TVShow.Seasons = LoadAllTVSeasonsDetailsFromDB(_TVDB.ID)
        End If

        'Episodes
        If bWithEpisodes Then
            For Each tEpisode As DBElement In Load_AllTVEpisodes(_TVDB.ID, False, -1, bWithMissingEpisodes)
                tEpisode = AddTVShowInfoToDBElement(tEpisode, _TVDB)
                _TVDB.Episodes.Add(tEpisode)
            Next
        End If

        'Check if the path is available and ready to edit
        If Directory.Exists(_TVDB.ShowPath) Then _TVDB.IsOnline = True

        Return _TVDB
    End Function

    Public Function Load_Path_TVShow(ByVal lngShowID As Long) As String
        Dim ShowPath As String = String.Empty

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT TVShowPath FROM tvshow WHERE idShow = ", lngShowID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.HasRows Then
                    SQLreader.Read()
                    ShowPath = SQLreader("TVShowPath").ToString
                End If
            End Using
        End Using

        Return ShowPath
    End Function

    Public Function Load_Source_TVShow(ByVal lngSourceID As Long) As DBSource
        Dim _source As New DBSource

        _source.ID = lngSourceID
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT * FROM tvshowsource WHERE idSource = ", _source.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                If SQLreader.HasRows Then
                    SQLreader.Read()
                    _source.Name = SQLreader("strName").ToString
                    _source.Path = SQLreader("strPath").ToString
                    _source.Language = SQLreader("strLanguage").ToString
                    _source.Ordering = DirectCast(Convert.ToInt32(SQLreader("iOrdering")), Enums.EpisodeOrdering)
                    _source.Exclude = Convert.ToBoolean(SQLreader("bExclude"))
                    _source.EpisodeSorting = DirectCast(Convert.ToInt32(SQLreader("iEpisodeSorting")), Enums.EpisodeSorting)
                    _source.LastScan = SQLreader("strLastScan").ToString
                    _source.IsSingle = Convert.ToBoolean(SQLreader("bSingle"))
                End If
            End Using
        End Using

        Return _source
    End Function
    ''' <summary>
    ''' Load TV Sources from the DB. This populates the Master.TVSources list of TV Sources
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub Load_Sources_TVShow()
        Master.TVShowSources.Clear()
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT * FROM tvshowsource;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Try ' Parsing database entry may fail. If it does, log the error and ignore the entry but continue processing
                        Dim tvsource As New DBSource
                        tvsource.ID = Convert.ToInt64(SQLreader("idSource"))
                        tvsource.Name = SQLreader("strName").ToString
                        tvsource.Path = SQLreader("strPath").ToString
                        tvsource.Language = SQLreader("strLanguage").ToString
                        tvsource.Ordering = DirectCast(Convert.ToInt32(SQLreader("iOrdering")), Enums.EpisodeOrdering)
                        tvsource.Exclude = Convert.ToBoolean(SQLreader("bExclude"))
                        tvsource.EpisodeSorting = DirectCast(Convert.ToInt32(SQLreader("iEpisodeSorting")), Enums.EpisodeSorting)
                        tvsource.LastScan = SQLreader("strLastScan").ToString
                        tvsource.IsSingle = Convert.ToBoolean(SQLreader("bSingle"))
                        Master.TVShowSources.Add(tvsource)
                    Catch ex As Exception
                        logger.Error(ex, New StackFrame().GetMethod().Name)
                    End Try
                End While
            End Using
        End Using
    End Sub

    Private Sub bwPatchDB_DoWork(ByVal sender As Object, ByVal e As System.ComponentModel.DoWorkEventArgs) Handles bwPatchDB.DoWork
        Dim Args As Arguments = DirectCast(e.Argument, Arguments)

        Dim xmlSer As XmlSerializer
        Dim _cmds As New Containers.InstallCommands
        Dim TransOk As Boolean
        Dim tempName As String = String.Empty

        tempName = String.Concat(Args.newDBPath, "_tmp")
        If File.Exists(tempName) Then
            File.Delete(tempName)
        End If
        File.Copy(Args.currDBPath, tempName)

        Try
            _myvideosDBConn = New SQLiteConnection(String.Format(_connStringTemplate, tempName))
            _myvideosDBConn.Open()

            For i As Integer = Args.currVersion To Args.newVersion - 1

                Dim patchpath As String = FileUtils.Common.ReturnSettingsFile("DB", String.Format("MyVideosDBSQL_v{0}_Patch.xml", i))

                xmlSer = New XmlSerializer(GetType(Containers.InstallCommands))
                Using xmlSW As New StreamReader(Path.Combine(Functions.AppPath, patchpath))
                    _cmds = DirectCast(xmlSer.Deserialize(xmlSW), Containers.InstallCommands)
                End Using

                For Each Trans In _cmds.transaction
                    TransOk = True
                    Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                        For Each _cmd As Containers.CommandsTransactionCommand In Trans.command
                            If _cmd.type = "DB" Then
                                Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                    SQLcommand.CommandText = _cmd.execute
                                    Try
                                        SQLcommand.ExecuteNonQuery()
                                        logger.Info(String.Concat(Trans.name, ": ", _cmd.description))
                                    Catch ex As Exception
                                        logger.Error(New StackFrame().GetMethod().Name, ex, Trans.name, _cmd.description)
                                        TransOk = False
                                        Exit For
                                    End Try
                                End Using
                            End If
                        Next
                        If TransOk Then
                            logger.Trace(String.Format("Transaction {0} Commit Done", Trans.name))
                            SQLtransaction.Commit()
                            ' Housekeeping - consolidate and pack database using vacuum command http://www.sqlite.org/lang_vacuum.html
                            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                SQLcommand.CommandText = "VACUUM;"
                                SQLcommand.ExecuteNonQuery()
                            End Using
                        Else
                            logger.Trace(New StackFrame().GetMethod().Name, String.Format("Transaction {0} RollBack", Trans.name))
                            SQLtransaction.Rollback()
                        End If
                    End Using
                Next
                For Each _cmd As Containers.CommandsNoTransactionCommand In _cmds.noTransaction
                    If _cmd.type = "DB" Then
                        Using SQLnotransaction As SQLiteCommand = _myvideosDBConn.CreateCommand()
                            SQLnotransaction.CommandText = _cmd.execute
                            Try
                                SQLnotransaction.ExecuteNonQuery()
                                ' Housekeeping - consolidate and pack database using vacuum command http://www.sqlite.org/lang_vacuum.html
                                Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                    SQLcommand.CommandText = "VACUUM;"
                                    SQLcommand.ExecuteNonQuery()
                                End Using
                            Catch ex As Exception
                                logger.Info(New StackFrame().GetMethod().Name, ex, SQLnotransaction, _cmd.description, _cmd.execute)
                            End Try
                        End Using
                    End If
                Next
            Next

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 14
                        PrepareTable_country("idMovie", "movie", True)
                        PrepareTable_director("idEpisode", "episode", True)
                        PrepareTable_director("idMovie", "movie", True)
                        PrepareTable_genre("idMovie", "movie", True)
                        PrepareTable_genre("idShow", "tvshow", True)
                        PrepareTable_studio("idMovie", "movie", True)
                        PrepareTable_studio("idShow", "tvshow", True)
                        PrepareTable_writer("idEpisode", "episode", True)
                        PrepareTable_writer("idMovie", "movie", True)
                        Prepare_Playcounts("episode", True)
                        Prepare_Playcounts("movie", True)
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 18
                        Prepare_VotesCount("idEpisode", "episode", True)
                        Prepare_VotesCount("idMovie", "movie", True)
                        Prepare_VotesCount("idShow", "tvshow", True)
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 21
                        Prepare_SortTitle("tvshow", True)
                        Prepare_DisplayEpisodeSeason(True)
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 26
                        Prepare_EFanartsPath("idMovie", "movie", True)
                        Prepare_EThumbsPath("idMovie", "movie", True)
                        Prepare_EFanartsPath("idShow", "tvshow", True)
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 30
                        Prepare_Language("movie", True)
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 31
                        Prepare_Language("sets", True)
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 40
                        Prepare_IMDB(True)
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 41
                        Prepare_Top250(True)
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 42
                        Prepare_OrphanedLinks(True)
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 43
                        If MessageBox.Show("Locked state will now be saved in NFO. Do you want to rewrite all NFOs of locked items?", "Rewrite NFOs of locked items", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                            Prepare_LockedStateToNFO(True)
                        End If
                End Select

                SQLtransaction.Commit()
            End Using

            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Select Case Args.currVersion
                    Case Is < 46
                        Prepare_TMDB_TMDBColID_TVDB(True)
                End Select

                SQLtransaction.Commit()
            End Using

            _myvideosDBConn.Close()
            File.Move(tempName, Args.newDBPath)
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name & Convert.ToChar(Windows.Forms.Keys.Tab) & "Unable to open media database connection.")
            _myvideosDBConn.Close()
        End Try
    End Sub

    Private Sub bwPatchDB_ProgressChanged(ByVal sender As Object, ByVal e As System.ComponentModel.ProgressChangedEventArgs) Handles bwPatchDB.ProgressChanged
        If e.ProgressPercentage = -1 Then
            Master.fLoading.SetLoadingMesg(e.UserState.ToString)
        End If
    End Sub

    Private Sub bwPatchDB_RunWorkerCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bwPatchDB.RunWorkerCompleted
        Return
    End Sub
    ''' <summary>
    ''' Execute arbitrary SQL commands against the database. Commands are retrieved from fname. 
    ''' Commands are serialized Containers.InstallCommands. Only commands marked as CommandType DB are executed.
    ''' </summary>
    ''' <param name="strCurrentPath">path to current DB</param>
    ''' <param name="strNewPath">path for new DB</param>
    ''' <param name="intCurrentVersion">current version of DB to patch</param>
    ''' <param name="intNewVersion">lastest version of DB</param>
    ''' <remarks></remarks>
    Public Sub Patch_MyVideos(ByVal strCurrentPath As String, ByVal strNewPath As String, ByVal intCurrentVersion As Integer, ByVal intNewVersion As Integer)

        Master.fLoading.SetProgressBarStyle(ProgressBarStyle.Marquee)

        bwPatchDB = New System.ComponentModel.BackgroundWorker
        bwPatchDB.WorkerReportsProgress = True
        bwPatchDB.WorkerSupportsCancellation = False
        bwPatchDB.RunWorkerAsync(New Arguments With {.currDBPath = strCurrentPath, .currVersion = intCurrentVersion, .newDBPath = strNewPath, .newVersion = intNewVersion})

        While bwPatchDB.IsBusy
            Application.DoEvents()
            Threading.Thread.Sleep(50)
        End While
    End Sub

    Private Sub PrepareTable_country(ByVal strIDFieldName As String, ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Get countries...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT {0}, country FROM {1};", strIDFieldName, strTable)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("country")) AndAlso Not String.IsNullOrEmpty(SQLreader("country").ToString) Then
                        Dim valuelist As New List(Of String)
                        Dim strValue As String = SQLreader("country").ToString
                        Dim idMedia As Long = Convert.ToInt64(SQLreader(strIDFieldName))

                        If strValue.Contains("/") Then
                            Dim values As String() = strValue.Split(New [Char]() {"/"c})
                            For Each value As String In values
                                value = value.Trim
                                If Not valuelist.Contains(value) Then
                                    valuelist.Add(value)
                                End If
                            Next
                        Else
                            strValue = strValue.Trim
                            If Not valuelist.Contains(strValue) Then
                                valuelist.Add(strValue.Trim)
                            End If
                        End If

                        For Each value As String In valuelist
                            Select Case strTable
                                Case "movie"
                                    AddCountryToMovie(idMedia, AddCountry(value))
                            End Select
                        Next
                    End If
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub PrepareTable_director(ByVal strIDFieldName As String, ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Get directors...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT {0}, director FROM {1};", strIDFieldName, strTable)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("director")) AndAlso Not String.IsNullOrEmpty(SQLreader("director").ToString) Then
                        Dim valuelist As New List(Of String)
                        Dim strValue As String = SQLreader("director").ToString
                        Dim idMedia As Long = Convert.ToInt64(SQLreader(strIDFieldName))

                        If strValue.Contains("/") Then
                            Dim values As String() = strValue.Split(New [Char]() {"/"c})
                            For Each value As String In values
                                value = value.Trim
                                If Not valuelist.Contains(value) Then
                                    valuelist.Add(value)
                                End If
                            Next
                        Else
                            strValue = strValue.Trim
                            If Not valuelist.Contains(strValue) Then
                                valuelist.Add(strValue.Trim)
                            End If
                        End If

                        For Each value As String In valuelist
                            Select Case strTable
                                Case "episode"
                                    AddDirectorToEpisode(idMedia, AddActor(value, "", "", "", -1, False))
                                Case "movie"
                                    AddDirectorToMovie(idMedia, AddActor(value, "", "", "", -1, False))
                                Case "tvshow"
                                    AddDirectorToTvShow(idMedia, AddActor(value, "", "", "", -1, False))
                            End Select
                        Next
                    End If
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub PrepareTable_genre(ByVal strIDFieldName As String, ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Get genres...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT {0}, genre FROM {1};", strIDFieldName, strTable)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("genre")) AndAlso Not String.IsNullOrEmpty(SQLreader("genre").ToString) Then
                        Dim valuelist As New List(Of String)
                        Dim strValue As String = SQLreader("genre").ToString
                        Dim idMedia As Long = Convert.ToInt64(SQLreader(strIDFieldName))

                        If strValue.Contains("/") Then
                            Dim values As String() = strValue.Split(New [Char]() {"/"c})
                            For Each value As String In values
                                value = value.Trim
                                If Not valuelist.Contains(value) Then
                                    valuelist.Add(value)
                                End If
                            Next
                        Else
                            strValue = strValue.Trim
                            If Not valuelist.Contains(strValue) Then
                                valuelist.Add(strValue.Trim)
                            End If
                        End If

                        For Each value As String In valuelist
                            Select Case strTable
                                Case "movie"
                                    AddGenreToMovie(idMedia, AddGenre(value))
                                Case "tvshow"
                                    AddGenreToTvShow(idMedia, AddGenre(value))
                            End Select
                        Next
                    End If
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub PrepareTable_studio(ByVal strIDFieldName As String, ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Get studios...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT {0}, studio FROM {1};", strIDFieldName, strTable)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("studio")) AndAlso Not String.IsNullOrEmpty(SQLreader("studio").ToString) Then
                        Dim valuelist As New List(Of String)
                        Dim strValue As String = SQLreader("studio").ToString
                        Dim idMedia As Long = Convert.ToInt64(SQLreader(strIDFieldName))

                        If strValue.Contains("/") Then
                            Dim values As String() = strValue.Split(New [Char]() {"/"c})
                            For Each value As String In values
                                value = value.Trim
                                If Not valuelist.Contains(value) Then
                                    valuelist.Add(value)
                                End If
                            Next
                        Else
                            strValue = strValue.Trim
                            If Not valuelist.Contains(strValue) Then
                                valuelist.Add(strValue.Trim)
                            End If
                        End If

                        For Each value As String In valuelist
                            Select Case strTable
                                Case "movie"
                                    AddStudioToMovie(idMedia, AddStudio(value))
                                Case "tvshow"
                                    AddStudioToTvShow(idMedia, AddStudio(value))
                            End Select
                        Next
                    End If
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub PrepareTable_writer(ByVal strIDFieldName As String, ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Get writers...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT {0}, credits FROM {1};", strIDFieldName, strTable)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("credits")) AndAlso Not String.IsNullOrEmpty(SQLreader("credits").ToString) Then
                        Dim valuelist As New List(Of String)
                        Dim strValue As String = SQLreader("credits").ToString
                        Dim idMedia As Long = Convert.ToInt64(SQLreader(strIDFieldName))

                        If strValue.Contains("/") Then
                            Dim values As String() = strValue.Split(New [Char]() {"/"c})
                            For Each value As String In values
                                value = value.Trim
                                If Not valuelist.Contains(value) Then
                                    valuelist.Add(value)
                                End If
                            Next
                        Else
                            strValue = strValue.Trim
                            If Not valuelist.Contains(strValue) Then
                                valuelist.Add(strValue.Trim)
                            End If
                        End If

                        For Each value As String In valuelist
                            Select Case strTable
                                Case "episode"
                                    AddWriterToEpisode(idMedia, AddActor(value, "", "", "", -1, False))
                                Case "movie"
                                    AddWriterToMovie(idMedia, AddActor(value, "", "", "", -1, False))
                            End Select
                        Next
                    End If
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_DisplayEpisodeSeason(ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Fixing DisplayEpisode and DisplaySeason...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "UPDATE episode SET DisplayEpisode = -1 WHERE DisplayEpisode IS NULL;"
            SQLcommand.ExecuteNonQuery()
            SQLcommand.CommandText = "UPDATE episode SET DisplaySeason = -1 WHERE DisplaySeason IS NULL;"
            SQLcommand.ExecuteNonQuery()
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_EFanartsPath(ByVal strIDFieldName As String, ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Fixing Extrafanarts Paths...")
        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT * FROM {0} WHERE EFanartsPath NOT LIKE ''", strTable)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Dim newExtrafanartsPath As String = String.Empty
                    If Not DBNull.Value.Equals(SQLreader("EFanartsPath")) Then newExtrafanartsPath = SQLreader("EFanartsPath").ToString
                    newExtrafanartsPath = Directory.GetParent(newExtrafanartsPath).FullName
                    Using SQLcommand_update_paths As SQLiteCommand = _myvideosDBConn.CreateCommand()
                        SQLcommand_update_paths.CommandText = String.Format("UPDATE {0} SET EFanartsPath=? WHERE {1}={2}", strTable, strIDFieldName, SQLreader(strIDFieldName))
                        Dim par_ExtrafanartsPath As SQLiteParameter = SQLcommand_update_paths.Parameters.Add("par_EFanartsPath", DbType.String, 0, "EFanartsPath")
                        par_ExtrafanartsPath.Value = newExtrafanartsPath
                        SQLcommand_update_paths.ExecuteNonQuery()
                    End Using
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_EThumbsPath(ByVal strIDFieldName As String, ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Fixing ExtrathumbsPaths...")
        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT * FROM {0} WHERE EThumbsPath NOT LIKE ''", strTable)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Dim newExtrathumbsPath As String = String.Empty
                    If Not DBNull.Value.Equals(SQLreader("EThumbsPath")) Then newExtrathumbsPath = SQLreader("EThumbsPath").ToString
                    newExtrathumbsPath = Directory.GetParent(newExtrathumbsPath).FullName
                    Using SQLcommand_update_paths As SQLiteCommand = _myvideosDBConn.CreateCommand()
                        SQLcommand_update_paths.CommandText = String.Format("UPDATE {0} SET EThumbsPath=? WHERE {1}={2}", strTable, strIDFieldName, SQLreader(strIDFieldName))
                        Dim par_ExtrathumbsPath As SQLiteParameter = SQLcommand_update_paths.Parameters.Add("par_EThumbsPath", DbType.String, 0, "EThumbsPath")
                        par_ExtrathumbsPath.Value = newExtrathumbsPath
                        SQLcommand_update_paths.ExecuteNonQuery()
                    End Using
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_Language(ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Set all languages to ""en-US"" ...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("UPDATE {0} SET Language = 'en-US';", strTable)
            SQLcommand.ExecuteNonQuery()
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_IMDB(ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Cleanup all IMDB ID's ...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT idMovie, Imdb FROM movie WHERE movie.Imdb <> '';"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("Imdb")) AndAlso Not String.IsNullOrEmpty(SQLreader("Imdb").ToString) AndAlso Not SQLreader("Imdb").ToString.StartsWith("tt") Then
                        Using SQLcommand_cleanup_imdb As SQLiteCommand = _myvideosDBConn.CreateCommand()
                            SQLcommand_cleanup_imdb.CommandText = String.Format("UPDATE movie SET Imdb=? WHERE idMovie={0}", SQLreader("idMovie").ToString)
                            Dim par_Imdb As SQLiteParameter = SQLcommand_cleanup_imdb.Parameters.Add("par_Imdb", DbType.String, 0, "Imdb")
                            par_Imdb.Value = String.Concat("tt", SQLreader("Imdb").ToString)
                            SQLcommand_cleanup_imdb.ExecuteNonQuery()
                        End Using
                    End If
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_LockedStateToNFO(ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Rewriting NFOs...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        'Movies
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT idMovie FROM movie WHERE Lock = 1;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Dim tmpDBElement As DBElement = Load_Movie(Convert.ToInt64(SQLreader("idMovie")))
                    Save_Movie(tmpDBElement, bBatchMode, True, False, False, False)
                End While
            End Using
        End Using

        'TVEpsiodes
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT idEpisode FROM episode WHERE Lock = 1;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Dim tmpDBElement As DBElement = Load_TVEpisode(Convert.ToInt64(SQLreader("idEpisode")), False)
                    Save_TVEpisode(tmpDBElement, bBatchMode, True, False, False, False)
                End While
            End Using
        End Using

        'TVShows
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "SELECT idShow FROM tvshow WHERE Lock = 1;"
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Dim tmpDBElement As DBElement = Load_TVShow(Convert.ToInt64(SQLreader("idShow")), False, False)
                    Save_TVShow(tmpDBElement, bBatchMode, True, False, False)
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_OrphanedLinks(ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Removing orphaned links...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            bwPatchDB.ReportProgress(-1, "Cleaning movie table")
            SQLcommand.CommandText = "DELETE FROM movie WHERE idSource NOT IN (SELECT idSource FROM moviesource);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning tvshow table")
            SQLcommand.CommandText = "DELETE FROM tvshow WHERE idSource NOT IN (SELECT idSource FROM tvshowsource);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning actorlinkmovie table")
            SQLcommand.CommandText = "DELETE FROM actorlinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning art table")
            SQLcommand.CommandText = "DELETE FROM art WHERE media_id NOT IN (SELECT idMovie FROM movie) AND media_type = 'movie';"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning countrylinkmovie table")
            SQLcommand.CommandText = "DELETE FROM countrylinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning directorlinkmovie table")
            SQLcommand.CommandText = "DELETE FROM directorlinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning genrelinkmovie table")
            SQLcommand.CommandText = "DELETE FROM genrelinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning movielinktvshow table")
            SQLcommand.CommandText = "DELETE FROM movielinktvshow WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning setlinkmovie table")
            SQLcommand.CommandText = "DELETE FROM setlinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning studiolinkmovie table")
            SQLcommand.CommandText = "DELETE FROM studiolinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning taglinks table")
            SQLcommand.CommandText = "DELETE FROM taglinks WHERE idMedia NOT IN (SELECT idMovie FROM movie) AND media_type = 'movie';"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning writerlinkmovie table")
            SQLcommand.CommandText = "DELETE FROM writerlinkmovie WHERE idMovie NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning MoviesAStreams table")
            SQLcommand.CommandText = "DELETE FROM MoviesAStreams WHERE MovieID NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning MoviesSubs table")
            SQLcommand.CommandText = "DELETE FROM MoviesSubs WHERE MovieID NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
            bwPatchDB.ReportProgress(-1, "Cleaning MoviesVStreams table")
            SQLcommand.CommandText = "DELETE FROM MoviesVStreams WHERE MovieID NOT IN (SELECT idMovie FROM movie);"
            SQLcommand.ExecuteNonQuery()
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_Playcounts(ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Fixing Playcounts...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("UPDATE {0} SET Playcount = NULL WHERE Playcount = 0 OR Playcount = """";", strTable)
            SQLcommand.ExecuteNonQuery()
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_SortTitle(ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Fixing SortTitles...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("UPDATE {0} SET SortTitle = '' WHERE SortTitle IS NULL OR SortTitle = """";", strTable)
            SQLcommand.ExecuteNonQuery()
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_TMDB_TMDBColID_TVDB(ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Fixing TMDB ID, TMDBColID and TVDB ID...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "UPDATE episode SET TMDB = NULL WHERE TMDB = 0 OR TMDB = """";"
            SQLcommand.ExecuteNonQuery()
            SQLcommand.CommandText = "UPDATE episode SET TVDB = NULL WHERE TVDB = 0 OR TVDB = """";"
            SQLcommand.ExecuteNonQuery()
            SQLcommand.CommandText = "UPDATE movie SET TMDB = NULL WHERE TMDB = 0 OR TMDB = """";"
            SQLcommand.ExecuteNonQuery()
            SQLcommand.CommandText = "UPDATE movie SET TMDBColID = NULL WHERE TMDBColID = 0 OR TMDBColID = """";"
            SQLcommand.ExecuteNonQuery()
            SQLcommand.CommandText = "UPDATE seasons SET TMDB = NULL WHERE TMDB = 0 OR TMDB = """";"
            SQLcommand.ExecuteNonQuery()
            SQLcommand.CommandText = "UPDATE seasons SET TVDB = NULL WHERE TVDB = 0 OR TVDB = """";"
            SQLcommand.ExecuteNonQuery()
            SQLcommand.CommandText = "UPDATE sets SET TMDBColID = NULL WHERE TMDBColID = 0 OR TMDBColID = """";"
            SQLcommand.ExecuteNonQuery()
            SQLcommand.CommandText = "UPDATE tvshow SET TMDB = NULL WHERE TMDB = 0 OR TMDB = """";"
            SQLcommand.ExecuteNonQuery()
            SQLcommand.CommandText = "UPDATE tvshow SET TVDB = NULL WHERE TVDB = 0 OR TVDB = """";"
            SQLcommand.ExecuteNonQuery()
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_Top250(ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Fixing Top250...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = "UPDATE movie SET Top250 = NULL WHERE Top250 = 0 OR Top250 = """";"
            SQLcommand.ExecuteNonQuery()
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    Private Sub Prepare_VotesCount(ByVal strIDFieldName As String, ByVal strTable As String, ByVal bBatchMode As Boolean)
        bwPatchDB.ReportProgress(-1, "Clean Votes count...")

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Format("SELECT {0}, Votes FROM {1};", strIDFieldName, strTable)
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    If Not DBNull.Value.Equals(SQLreader("Votes")) AndAlso Not String.IsNullOrEmpty(SQLreader("Votes").ToString) AndAlso Not Integer.TryParse(SQLreader("Votes").ToString, 0) Then
                        Using SQLcommand_update_votes As SQLiteCommand = _myvideosDBConn.CreateCommand()
                            SQLcommand_update_votes.CommandText = String.Format("UPDATE {0} SET Votes=? WHERE {1}={2}", strTable, strIDFieldName, SQLreader(strIDFieldName))
                            Dim par_update_Votes As SQLiteParameter = SQLcommand_update_votes.Parameters.Add("par_update_Votes", DbType.String, 0, "Vote")
                            par_update_Votes.Value = NumUtils.CleanVotes(SQLreader("Votes").ToString)
                            SQLcommand_update_votes.ExecuteNonQuery()
                        End Using
                    End If
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub

    '  Public Function CheckEssentials() As Boolean
    'Dim needUpdate As Boolean = False
    'Dim lhttp As New HTTP
    'If Not File.Exists(Path.Combine(Functions.AppPath, "Media.emm")) Then
    '	System.IO.File.Copy(Path.Combine(Path.Combine(Functions.AppPath, "Resources"), "commands_base.xml"), Path.Combine(Functions.AppPath, "InstallTasks.xml"))
    '	'lhttp.DownloadFile(String.Format("http://pcjco.dommel.be/emm-r/{0}/commands_base.xml", If(Functions.IsBetaEnabled(), "updatesbeta", "updates")), Path.Combine(Functions.AppPath, "InstallTasks.xml"), False, "other")
    'End If
    'If File.Exists(Path.Combine(Functions.AppPath, "InstallTasks.xml")) Then
    '	Master.DB.PatchDatabase("InstallTasks.xml")
    '	File.Delete(Path.Combine(Functions.AppPath, "InstallTasks.xml"))
    '	needUpdate = True
    'End If
    'If File.Exists(Path.Combine(Functions.AppPath, "UpdateTasks.xml")) Then
    '	Master.DB.PatchDatabase("UpdateTasks.xml")
    '	File.Delete(Path.Combine(Functions.AppPath, "UpdateTasks.xml"))
    '	needUpdate = True
    'End If
    'Return needUpdate
    '  End Function

    ''' <summary>
    ''' Saves all information from a Database.DBElement object to the database
    ''' </summary>
    ''' <param name="tDBElement">Media.Movie object to save to the database</param>
    ''' <param name="bBatchMode">Is the function already part of a transaction?</param>
    ''' <param name="bToNFO">Save informations to NFO</param>
    ''' <param name="bToDisk">Save Images, Themes and Trailers to disk</param>
    ''' <returns>Database.DBElement object</returns>
    Public Function Save_Movie(ByVal tDBElement As DBElement, ByVal bBatchMode As Boolean, ByVal bToNFO As Boolean, ByVal bToDisk As Boolean, ByVal bDoSync As Boolean, ByVal bForceFileCleanup As Boolean) As DBElement
        If tDBElement.MainDetails Is Nothing Then Return tDBElement

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
        Using SQLcommand_movie As SQLiteCommand = _myvideosDBConn.CreateCommand()
            If Not tDBElement.IDSpecified Then
                SQLcommand_movie.CommandText = String.Concat("INSERT OR REPLACE INTO movie (",
                 "idSource, MoviePath, Type, ListTitle, HasSub, New, Mark, IMDB, Lock, ",
                 "Title, OriginalTitle, SortTitle, Year, Rating, Votes, MPAA, Top250, Outline, Plot, Tagline, Certification, ",
                 "Runtime, ReleaseDate, Playcount, Trailer, ",
                 "NfoPath, TrailerPath, SubPath, EThumbsPath, FanartURL, OutOfTolerance, VideoSource, ",
                 "DateAdded, EFanartsPath, ThemePath, ",
                 "TMDB, TMDBColID, DateModified, MarkCustom1, MarkCustom2, MarkCustom3, MarkCustom4, HasSet, LastPlayed, Language, UserRating",
                 ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM movie;")
            Else
                SQLcommand_movie.CommandText = String.Concat("INSERT OR REPLACE INTO movie (",
                 "idMovie, idSource, MoviePath, Type, ListTitle, HasSub, New, Mark, IMDB, Lock, ",
                 "Title, OriginalTitle, SortTitle, Year, Rating, Votes, MPAA, Top250, Outline, Plot, Tagline, Certification, ",
                 "Runtime, ReleaseDate, Playcount, Trailer, ",
                 "NfoPath, TrailerPath, SubPath, EThumbsPath, FanartURL, OutOfTolerance, VideoSource, ",
                 "DateAdded, EFanartsPath, ThemePath, ",
                 "TMDB, TMDBColID, DateModified, MarkCustom1, MarkCustom2, MarkCustom3, MarkCustom4, HasSet, LastPlayed, Language, UserRating",
                 ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM movie;")
                Dim parMovieID As SQLiteParameter = SQLcommand_movie.Parameters.Add("paridMovie", DbType.Int64, 0, "idMovie")
                parMovieID.Value = tDBElement.ID
            End If
            Dim par_movie_idSource As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_idSource", DbType.Int64, 0, "idSource")
            Dim par_movie_MoviePath As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_MoviePath", DbType.String, 0, "MoviePath")
            Dim par_movie_Type As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Type", DbType.Boolean, 0, "Type")
            Dim par_movie_ListTitle As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_ListTitle", DbType.String, 0, "ListTitle")
            Dim par_movie_HasSub As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_HasSub", DbType.Boolean, 0, "HasSub")
            Dim par_movie_New As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_New", DbType.Boolean, 0, "New")
            Dim par_movie_Mark As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Mark", DbType.Boolean, 0, "Mark")
            Dim par_movie_IMDB As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_IMDB", DbType.String, 0, "IMDB")
            Dim par_movie_Lock As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Lock", DbType.Boolean, 0, "Lock")
            Dim par_movie_Title As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Title", DbType.String, 0, "Title")
            Dim par_movie_OriginalTitle As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_OriginalTitle", DbType.String, 0, "OriginalTitle")
            Dim par_movie_SortTitle As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_SortTitle", DbType.String, 0, "SortTitle")
            Dim par_movie_Year As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Year", DbType.String, 0, "Year")
            Dim par_movie_Rating As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Rating", DbType.String, 0, "Rating")
            Dim par_movie_Votes As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Votes", DbType.String, 0, "Votes")
            Dim par_movie_MPAA As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_MPAA", DbType.String, 0, "MPAA")
            Dim par_movie_Top250 As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Top250", DbType.Int64, 0, "Top250")
            Dim par_movie_Outline As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Outline", DbType.String, 0, "Outline")
            Dim par_movie_Plot As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Plot", DbType.String, 0, "Plot")
            Dim par_movie_Tagline As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Tagline", DbType.String, 0, "Tagline")
            Dim par_movie_Certification As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Certification", DbType.String, 0, "Certification")
            Dim par_movie_Runtime As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Runtime", DbType.String, 0, "Runtime")
            Dim par_movie_ReleaseDate As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_ReleaseDate", DbType.String, 0, "ReleaseDate")
            Dim par_movie_Playcount As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Playcount", DbType.Int64, 0, "Playcount")
            Dim par_movie_Trailer As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Trailer", DbType.String, 0, "Trailer")
            Dim par_movie_NfoPath As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_NfoPath", DbType.String, 0, "NfoPath")
            Dim par_movie_TrailerPath As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_TrailerPath", DbType.String, 0, "TrailerPath")
            Dim par_movie_SubPath As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_SubPath", DbType.String, 0, "SubPath")
            Dim par_movie_ExtrathumbsPath As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_EThumbsPath", DbType.String, 0, "EThumbsPath")
            Dim par_movie_FanartURL As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_FanartURL", DbType.String, 0, "FanartURL")
            Dim par_movie_OutOfTolerance As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_OutOfTolerance", DbType.Boolean, 0, "OutOfTolerance")
            Dim par_movie_VideoSource As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_VideoSource", DbType.String, 0, "VideoSource")
            Dim par_movie_DateAdded As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_DateAdded", DbType.Int64, 0, "DateAdded")
            Dim par_movie_ExtrafanartsPath As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_EFanartsPath", DbType.String, 0, "EFanartsPath")
            Dim par_movie_ThemePath As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_ThemePath", DbType.String, 0, "ThemePath")
            Dim par_movie_TMDB As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_TMDB", DbType.Int32, 0, "TMDB")
            Dim par_movie_TMDBColID As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_TMDBColID", DbType.Int32, 0, "TMDBColID")
            Dim par_movie_DateModified As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_DateModified", DbType.Int64, 0, "DateModified")
            Dim par_movie_MarkCustom1 As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_MarkCustom1", DbType.Boolean, 0, "MarkCustom1")
            Dim par_movie_MarkCustom2 As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_MarkCustom2", DbType.Boolean, 0, "MarkCustom2")
            Dim par_movie_MarkCustom3 As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_MarkCustom3", DbType.Boolean, 0, "MarkCustom3")
            Dim par_movie_MarkCustom4 As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_MarkCustom4", DbType.Boolean, 0, "MarkCustom4")
            Dim par_movie_HasSet As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_HasSet", DbType.Boolean, 0, "HasSet")
            Dim par_movie_LastPlayed As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_LastPlayed", DbType.Int64, 0, "LastPlayed")
            Dim par_movie_Language As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_Language", DbType.String, 0, "Language")
            Dim par_movie_UserRating As SQLiteParameter = SQLcommand_movie.Parameters.Add("par_movie_UserRating", DbType.Int64, 0, "UserRating")

            'DateAdded
            Try
                If Not Master.eSettings.GeneralDateAddedIgnoreNFO AndAlso Not String.IsNullOrEmpty(tDBElement.MainDetails.DateAdded) Then
                    Dim DateTimeAdded As Date = Date.ParseExact(tDBElement.MainDetails.DateAdded, "yyyy-MM-dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
                    par_movie_DateAdded.Value = Functions.ConvertToUnixTimestamp(DateTimeAdded)
                Else
                    Select Case Master.eSettings.GeneralDateTime
                        Case Enums.DateTime.Now
                            par_movie_DateAdded.Value = If(Not tDBElement.IDSpecified, Functions.ConvertToUnixTimestamp(Date.Now), tDBElement.DateAdded)
                        Case Enums.DateTime.ctime
                            Dim ctime As Date = File.GetCreationTime(tDBElement.FileItem.FirstStackedPath)
                            If ctime.Year > 1601 Then
                                par_movie_DateAdded.Value = Functions.ConvertToUnixTimestamp(ctime)
                            Else
                                Dim mtime As Date = File.GetLastWriteTime(tDBElement.FileItem.FirstStackedPath)
                                par_movie_DateAdded.Value = Functions.ConvertToUnixTimestamp(mtime)
                            End If
                        Case Enums.DateTime.mtime
                            Dim mtime As Date = File.GetLastWriteTime(tDBElement.FileItem.FirstStackedPath)
                            If mtime.Year > 1601 Then
                                par_movie_DateAdded.Value = Functions.ConvertToUnixTimestamp(mtime)
                            Else
                                Dim ctime As Date = File.GetCreationTime(tDBElement.FileItem.FirstStackedPath)
                                par_movie_DateAdded.Value = Functions.ConvertToUnixTimestamp(ctime)
                            End If
                        Case Enums.DateTime.Newer
                            Dim mtime As Date = File.GetLastWriteTime(tDBElement.FileItem.FirstStackedPath)
                            Dim ctime As Date = File.GetCreationTime(tDBElement.FileItem.FirstStackedPath)
                            If mtime > ctime Then
                                par_movie_DateAdded.Value = Functions.ConvertToUnixTimestamp(mtime)
                            Else
                                par_movie_DateAdded.Value = Functions.ConvertToUnixTimestamp(ctime)
                            End If
                    End Select
                End If
                tDBElement.MainDetails.DateAdded = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(par_movie_DateAdded.Value)).ToString("yyyy-MM-dd HH:mm:ss")
            Catch
                par_movie_DateAdded.Value = If(Not tDBElement.IDSpecified, Functions.ConvertToUnixTimestamp(Date.Now), tDBElement.DateAdded)
                tDBElement.MainDetails.DateAdded = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(par_movie_DateAdded.Value)).ToString("yyyy-MM-dd HH:mm:ss")
            End Try

            'DateModified
            Try
                If Not tDBElement.IDSpecified AndAlso tDBElement.MainDetails.DateModifiedSpecified Then
                    Dim DateTimeDateModified As Date = Date.ParseExact(tDBElement.MainDetails.DateModified, "yyyy-MM-dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
                    par_movie_DateModified.Value = Functions.ConvertToUnixTimestamp(DateTimeDateModified)
                ElseIf tDBElement.IDSpecified Then
                    par_movie_DateModified.Value = Functions.ConvertToUnixTimestamp(Date.Now)
                End If
                If par_movie_DateModified.Value IsNot Nothing Then
                    tDBElement.MainDetails.DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(par_movie_DateModified.Value)).ToString("yyyy-MM-dd HH:mm:ss")
                Else
                    tDBElement.MainDetails.DateModified = String.Empty
                End If
            Catch
                par_movie_DateModified.Value = If(Not tDBElement.IDSpecified, Functions.ConvertToUnixTimestamp(Date.Now), tDBElement.DateModified)
                tDBElement.MainDetails.DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(par_movie_DateModified.Value)).ToString("yyyy-MM-dd HH:mm:ss")
            End Try

            'LastPlayed
            Dim DateTimeLastPlayedUnix As Double = -1
            If tDBElement.MainDetails.LastPlayedSpecified Then
                Try
                    Dim DateTimeLastPlayed As Date = Date.ParseExact(tDBElement.MainDetails.LastPlayed, "yyyy-MM-dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
                    DateTimeLastPlayedUnix = Functions.ConvertToUnixTimestamp(DateTimeLastPlayed)
                Catch
                    'Kodi save it only as yyyy-MM-dd, try that
                    Try
                        Dim DateTimeLastPlayed As Date = Date.ParseExact(tDBElement.MainDetails.LastPlayed, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
                        DateTimeLastPlayedUnix = Functions.ConvertToUnixTimestamp(DateTimeLastPlayed)
                    Catch
                        DateTimeLastPlayedUnix = -1
                    End Try
                End Try
            End If
            If DateTimeLastPlayedUnix >= 0 Then
                par_movie_LastPlayed.Value = DateTimeLastPlayedUnix
            Else
                par_movie_LastPlayed.Value = Nothing 'need to be NOTHING instead of 0
                tDBElement.MainDetails.LastPlayed = String.Empty
            End If

            'Trailer URL
            If Master.eSettings.Movie.DataSettings.TrailerKodiFormat Then
                tDBElement.MainDetails.Trailer = tDBElement.MainDetails.Trailer.Trim.Replace("http://www.youtube.com/watch?v=", "plugin://plugin.video.youtube/?action=play_video&videoid=")
                tDBElement.MainDetails.Trailer = tDBElement.MainDetails.Trailer.Replace("http://www.youtube.com/watch?hd=1&v=", "plugin://plugin.video.youtube/?action=play_video&videoid=")
            End If

            'First let's save it to NFO, even because we will need the NFO path
            'Also save Images to get ExtrafanartsPath and ExtrathumbsPath
            'art Table will be linked later
            If bToNFO Then NFO.SaveToNFO_Movie(tDBElement, bForceFileCleanup)
            If bToDisk Then
                tDBElement.ImagesContainer.SaveAllImages(tDBElement, bForceFileCleanup)
                tDBElement.MainDetails.SaveAllActorThumbs(tDBElement)
                tDBElement.Theme.SaveAllThemes(tDBElement, bForceFileCleanup)
                tDBElement.Trailer.SaveAllTrailers(tDBElement, bForceFileCleanup)
            End If

            par_movie_MoviePath.Value = tDBElement.FileItem.FullPath
            par_movie_Type.Value = tDBElement.IsSingle
            par_movie_ListTitle.Value = tDBElement.ListTitle

            par_movie_ExtrafanartsPath.Value = tDBElement.ExtrafanartsPath
            par_movie_ExtrathumbsPath.Value = tDBElement.ExtrathumbsPath
            par_movie_NfoPath.Value = tDBElement.NfoPath
            par_movie_ThemePath.Value = If(Not String.IsNullOrEmpty(tDBElement.Theme.LocalFilePath), tDBElement.Theme.LocalFilePath, String.Empty)
            par_movie_TrailerPath.Value = If(Not String.IsNullOrEmpty(tDBElement.Trailer.LocalFilePath), tDBElement.Trailer.LocalFilePath, String.Empty)

            If Not Master.eSettings.Movie.ImageSettings.ImagesNotSaveURLToNfo Then
                par_movie_FanartURL.Value = tDBElement.MainDetails.Fanart.URL
            Else
                par_movie_FanartURL.Value = String.Empty
            End If

            par_movie_HasSet.Value = tDBElement.MainDetails.SetsSpecified
            If tDBElement.Subtitles IsNot Nothing Then
                par_movie_HasSub.Value = tDBElement.Subtitles.Count > 0 OrElse tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle.Count > 0
            Else
                par_movie_HasSub.Value = Nothing
            End If

            par_movie_Lock.Value = tDBElement.IsLock
            par_movie_Mark.Value = tDBElement.IsMark
            par_movie_MarkCustom1.Value = tDBElement.IsMarkCustom1
            par_movie_MarkCustom2.Value = tDBElement.IsMarkCustom2
            par_movie_MarkCustom3.Value = tDBElement.IsMarkCustom3
            par_movie_MarkCustom4.Value = tDBElement.IsMarkCustom4
            par_movie_New.Value = Not tDBElement.IDSpecified

            With tDBElement.MainDetails
                par_movie_Certification.Value = String.Join(" / ", .Certifications.ToArray)
                par_movie_IMDB.Value = .IMDB
                par_movie_UserRating.Value = .UserRating
                par_movie_MPAA.Value = .MPAA
                par_movie_OriginalTitle.Value = .OriginalTitle
                par_movie_Outline.Value = .Outline
                If .PlayCountSpecified Then 'need to be NOTHING instead of "0"
                    par_movie_Playcount.Value = .PlayCount
                End If
                par_movie_Plot.Value = .Plot
                par_movie_Rating.Value = .Rating
                par_movie_ReleaseDate.Value = NumUtils.DateToISO8601Date(.ReleaseDate)
                par_movie_Runtime.Value = .Runtime
                par_movie_SortTitle.Value = .SortTitle
                If .TMDBSpecified Then 'need to be NOTHING instead of "0"
                    par_movie_TMDB.Value = .TMDB
                End If
                If .TMDBColIDSpecified Then 'need to be NOTHING instead of "0"
                    par_movie_TMDBColID.Value = .TMDBColID
                End If
                par_movie_Tagline.Value = .Tagline
                par_movie_Title.Value = .Title
                If .Top250Specified Then 'need to be NOTHING instead of "0"
                    par_movie_Top250.Value = .Top250
                End If
                par_movie_Trailer.Value = .Trailer
                par_movie_Votes.Value = .Votes
                par_movie_Year.Value = .Year
            End With

            par_movie_OutOfTolerance.Value = tDBElement.OutOfTolerance
            par_movie_VideoSource.Value = tDBElement.VideoSource
            par_movie_Language.Value = tDBElement.Language

            par_movie_idSource.Value = tDBElement.Source.ID

            If Not tDBElement.IDSpecified Then
                If Master.eSettings.MovieGeneralMarkNew Then
                    par_movie_Mark.Value = True
                    tDBElement.IsMark = True
                End If
                Using rdrMovie As SQLiteDataReader = SQLcommand_movie.ExecuteReader()
                    If rdrMovie.Read Then
                        tDBElement.ID = Convert.ToInt64(rdrMovie(0))
                    Else
                        logger.Error("Something very wrong here: SaveMovieToDB", tDBElement.ToString)
                        tDBElement.ID = -1
                        Return tDBElement
                    End If
                End Using
            Else
                SQLcommand_movie.ExecuteNonQuery()
            End If

            If tDBElement.IDSpecified Then

                'Actors
                Using SQLcommand_actorlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_actorlink.CommandText = String.Format("DELETE FROM actorlinkmovie WHERE idMovie = {0};", tDBElement.ID)
                    SQLcommand_actorlink.ExecuteNonQuery()
                End Using
                AddCast(tDBElement.ID, "movie", "movie", tDBElement.MainDetails.Actors)

                'Countries
                Using SQLcommand_countrylink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_countrylink.CommandText = String.Format("DELETE FROM countrylinkmovie WHERE idMovie = {0};", tDBElement.ID)
                    SQLcommand_countrylink.ExecuteNonQuery()
                End Using
                For Each country As String In tDBElement.MainDetails.Countries
                    AddCountryToMovie(tDBElement.ID, AddCountry(country))
                Next

                'Directors
                Using SQLcommand_directorlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_directorlink.CommandText = String.Format("DELETE FROM directorlinkmovie WHERE idMovie = {0};", tDBElement.ID)
                    SQLcommand_directorlink.ExecuteNonQuery()
                End Using
                For Each director As String In tDBElement.MainDetails.Directors
                    AddDirectorToMovie(tDBElement.ID, AddActor(director, "", "", "", -1, False))
                Next

                'Genres
                Using SQLcommand_genrelink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_genrelink.CommandText = String.Format("DELETE FROM genrelinkmovie WHERE idMovie = {0};", tDBElement.ID)
                    SQLcommand_genrelink.ExecuteNonQuery()
                End Using
                For Each genre As String In tDBElement.MainDetails.Genres
                    AddGenreToMovie(tDBElement.ID, AddGenre(genre))
                Next

                'Images
                Using SQLcommand_art As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_art.CommandText = String.Format("DELETE FROM art WHERE media_id = {0} AND media_type = 'movie';", tDBElement.ID)
                    SQLcommand_art.ExecuteNonQuery()
                End Using
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Banner.LocalFilePath) Then SetArtForItem(tDBElement.ID, "movie", "banner", tDBElement.ImagesContainer.Banner.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.ClearArt.LocalFilePath) Then SetArtForItem(tDBElement.ID, "movie", "clearart", tDBElement.ImagesContainer.ClearArt.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.ClearLogo.LocalFilePath) Then SetArtForItem(tDBElement.ID, "movie", "clearlogo", tDBElement.ImagesContainer.ClearLogo.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.DiscArt.LocalFilePath) Then SetArtForItem(tDBElement.ID, "movie", "discart", tDBElement.ImagesContainer.DiscArt.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Fanart.LocalFilePath) Then SetArtForItem(tDBElement.ID, "movie", "fanart", tDBElement.ImagesContainer.Fanart.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Landscape.LocalFilePath) Then SetArtForItem(tDBElement.ID, "movie", "landscape", tDBElement.ImagesContainer.Landscape.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Poster.LocalFilePath) Then SetArtForItem(tDBElement.ID, "movie", "poster", tDBElement.ImagesContainer.Poster.LocalFilePath)

                'ShowLinks
                Using SQLcommand_showlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_showlink.CommandText = String.Format("DELETE FROM movielinktvshow WHERE idMovie = {0};", tDBElement.ID)
                    SQLcommand_showlink.ExecuteNonQuery()
                End Using
                For Each showlink As String In tDBElement.MainDetails.ShowLinks
                    AddTVShowToMovie(tDBElement.ID, showlink)
                Next

                'Studios
                Using SQLcommand_studiolink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_studiolink.CommandText = String.Format("DELETE FROM studiolinkmovie WHERE idMovie = {0};", tDBElement.ID)
                    SQLcommand_studiolink.ExecuteNonQuery()
                End Using
                For Each studio As String In tDBElement.MainDetails.Studios
                    AddStudioToMovie(tDBElement.ID, AddStudio(studio))
                Next

                'Tags
                Using SQLcommand_taglinks As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_taglinks.CommandText = String.Format("DELETE FROM taglinks WHERE idMedia = {0} AND media_type = 'movie';", tDBElement.ID)
                    SQLcommand_taglinks.ExecuteNonQuery()
                End Using
                For Each tag As String In tDBElement.MainDetails.Tags
                    AddTagToItem(tDBElement.ID, AddTag(tag), "movie")
                Next

                'Writers
                Using SQLcommand_writerlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_writerlink.CommandText = String.Format("DELETE FROM writerlinkmovie WHERE idMovie = {0};", tDBElement.ID)
                    SQLcommand_writerlink.ExecuteNonQuery()
                End Using
                For Each writer As String In tDBElement.MainDetails.Credits
                    AddWriterToMovie(tDBElement.ID, AddActor(writer, "", "", "", -1, False))
                Next

                'Video Streams
                Using SQLcommandMoviesVStreams As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommandMoviesVStreams.CommandText = String.Format("DELETE FROM MoviesVStreams WHERE MovieID = {0};", tDBElement.ID)
                    SQLcommandMoviesVStreams.ExecuteNonQuery()

                    'Expanded SQL Statement to INSERT/replace new fields
                    SQLcommandMoviesVStreams.CommandText = String.Concat("INSERT OR REPLACE INTO MoviesVStreams (",
                       "MovieID, StreamID, Video_Width,Video_Height,Video_Codec,Video_Duration, Video_ScanType, Video_AspectDisplayRatio, ",
                       "Video_Language, Video_LongLanguage, Video_Bitrate, Video_MultiViewCount, Video_FileSize, Video_MultiViewLayout, ",
                       "Video_StereoMode) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);")

                    Dim parVideo_MovieID As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_MovieID", DbType.Int64, 0, "MovieID")
                    Dim parVideo_StreamID As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_StreamID", DbType.Int64, 0, "StreamID")
                    Dim parVideo_Width As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_Width", DbType.String, 0, "Video_Width")
                    Dim parVideo_Height As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_Height", DbType.String, 0, "Video_Height")
                    Dim parVideo_Codec As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_Codec", DbType.String, 0, "Video_Codec")
                    Dim parVideo_Duration As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_Duration", DbType.String, 0, "Video_Duration")
                    Dim parVideo_ScanType As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_ScanType", DbType.String, 0, "Video_ScanType")
                    Dim parVideo_AspectDisplayRatio As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_AspectDisplayRatio", DbType.String, 0, "Video_AspectDisplayRatio")
                    Dim parVideo_Language As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_Language", DbType.String, 0, "Video_Language")
                    Dim parVideo_LongLanguage As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_LongLanguage", DbType.String, 0, "Video_LongLanguage")
                    Dim parVideo_Bitrate As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_Bitrate", DbType.String, 0, "Video_Bitrate")
                    Dim parVideo_MultiViewCount As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_MultiViewCount", DbType.String, 0, "Video_MultiViewCount")
                    Dim parVideo_FileSize As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_FileSize", DbType.Int64, 0, "Video_FileSize")
                    Dim parVideo_MultiViewLayout As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_MultiViewLayout", DbType.String, 0, "Video_MultiViewLayout")
                    Dim parVideo_StereoMode As SQLiteParameter = SQLcommandMoviesVStreams.Parameters.Add("parVideo_StereoMode", DbType.String, 0, "Video_StereoMode")

                    For i As Integer = 0 To tDBElement.MainDetails.FileInfo.StreamDetails.Video.Count - 1
                        parVideo_MovieID.Value = tDBElement.ID
                        parVideo_StreamID.Value = i
                        parVideo_Width.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Width
                        parVideo_Height.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Height
                        parVideo_Codec.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Codec
                        parVideo_Duration.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Duration
                        parVideo_ScanType.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Scantype
                        parVideo_AspectDisplayRatio.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Aspect
                        parVideo_Language.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Language
                        parVideo_LongLanguage.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).LongLanguage
                        parVideo_Bitrate.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Bitrate
                        parVideo_MultiViewCount.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).MultiViewCount
                        parVideo_MultiViewLayout.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).MultiViewLayout
                        parVideo_FileSize.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Filesize
                        parVideo_StereoMode.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).StereoMode

                        SQLcommandMoviesVStreams.ExecuteNonQuery()
                    Next
                End Using
                Using SQLcommandMoviesAStreams As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommandMoviesAStreams.CommandText = String.Concat("DELETE FROM MoviesAStreams WHERE MovieID = ", tDBElement.ID, ";")
                    SQLcommandMoviesAStreams.ExecuteNonQuery()

                    'Expanded SQL Statement to INSERT/replace new fields
                    SQLcommandMoviesAStreams.CommandText = String.Concat("INSERT OR REPLACE INTO MoviesAStreams (",
                      "MovieID, StreamID, Audio_Language, Audio_LongLanguage, Audio_Codec, Audio_Channel, Audio_Bitrate",
                      ") VALUES (?,?,?,?,?,?,?);")

                    Dim parAudio_MovieID As SQLiteParameter = SQLcommandMoviesAStreams.Parameters.Add("parAudio_MovieID", DbType.Int64, 0, "MovieID")
                    Dim parAudio_StreamID As SQLiteParameter = SQLcommandMoviesAStreams.Parameters.Add("parAudio_StreamID", DbType.Int64, 0, "StreamID")
                    Dim parAudio_Language As SQLiteParameter = SQLcommandMoviesAStreams.Parameters.Add("parAudio_Language", DbType.String, 0, "Audio_Language")
                    Dim parAudio_LongLanguage As SQLiteParameter = SQLcommandMoviesAStreams.Parameters.Add("parAudio_LongLanguage", DbType.String, 0, "Audio_LongLanguage")
                    Dim parAudio_Codec As SQLiteParameter = SQLcommandMoviesAStreams.Parameters.Add("parAudio_Codec", DbType.String, 0, "Audio_Codec")
                    Dim parAudio_Channel As SQLiteParameter = SQLcommandMoviesAStreams.Parameters.Add("parAudio_Channel", DbType.String, 0, "Audio_Channel")
                    Dim parAudio_Bitrate As SQLiteParameter = SQLcommandMoviesAStreams.Parameters.Add("parAudio_Bitrate", DbType.String, 0, "Audio_Bitrate")

                    For i As Integer = 0 To tDBElement.MainDetails.FileInfo.StreamDetails.Audio.Count - 1
                        parAudio_MovieID.Value = tDBElement.ID
                        parAudio_StreamID.Value = i
                        parAudio_Language.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).Language
                        parAudio_LongLanguage.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).LongLanguage
                        parAudio_Codec.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).Codec
                        parAudio_Channel.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).Channels
                        parAudio_Bitrate.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).Bitrate

                        SQLcommandMoviesAStreams.ExecuteNonQuery()
                    Next
                End Using

                'subtitles
                Using SQLcommandMoviesSubs As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommandMoviesSubs.CommandText = String.Concat("DELETE FROM MoviesSubs WHERE MovieID = ", tDBElement.ID, ";")
                    SQLcommandMoviesSubs.ExecuteNonQuery()

                    SQLcommandMoviesSubs.CommandText = String.Concat("INSERT OR REPLACE INTO MoviesSubs (",
                       "MovieID, StreamID, Subs_Language, Subs_LongLanguage,Subs_Type, Subs_Path, Subs_Forced",
                       ") VALUES (?,?,?,?,?,?,?);")
                    Dim parSubs_MovieID As SQLiteParameter = SQLcommandMoviesSubs.Parameters.Add("parSubs_MovieID", DbType.Int64, 0, "MovieID")
                    Dim parSubs_StreamID As SQLiteParameter = SQLcommandMoviesSubs.Parameters.Add("parSubs_StreamID", DbType.Int64, 0, "StreamID")
                    Dim parSubs_Language As SQLiteParameter = SQLcommandMoviesSubs.Parameters.Add("parSubs_Language", DbType.String, 0, "Subs_Language")
                    Dim parSubs_LongLanguage As SQLiteParameter = SQLcommandMoviesSubs.Parameters.Add("parSubs_LongLanguage", DbType.String, 0, "Subs_LongLanguage")
                    Dim parSubs_Type As SQLiteParameter = SQLcommandMoviesSubs.Parameters.Add("parSubs_Type", DbType.String, 0, "Subs_Type")
                    Dim parSubs_Path As SQLiteParameter = SQLcommandMoviesSubs.Parameters.Add("parSubs_Path", DbType.String, 0, "Subs_Path")
                    Dim parSubs_Forced As SQLiteParameter = SQLcommandMoviesSubs.Parameters.Add("parSubs_Forced", DbType.Boolean, 0, "Subs_Forced")
                    Dim iID As Integer = 0
                    'embedded subtitles
                    For i As Integer = 0 To tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle.Count - 1
                        parSubs_MovieID.Value = tDBElement.ID
                        parSubs_StreamID.Value = iID
                        parSubs_Language.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).Language
                        parSubs_LongLanguage.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).LongLanguage
                        parSubs_Type.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).SubsType
                        parSubs_Path.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).SubsPath
                        parSubs_Forced.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).SubsForced
                        SQLcommandMoviesSubs.ExecuteNonQuery()
                        iID += 1
                    Next
                    'external subtitles
                    For i As Integer = 0 To tDBElement.Subtitles.Count - 1
                        parSubs_MovieID.Value = tDBElement.ID
                        parSubs_StreamID.Value = iID
                        parSubs_Language.Value = tDBElement.Subtitles(i).Language
                        parSubs_LongLanguage.Value = tDBElement.Subtitles(i).LongLanguage
                        parSubs_Type.Value = tDBElement.Subtitles(i).SubsType
                        parSubs_Path.Value = tDBElement.Subtitles(i).SubsPath
                        parSubs_Forced.Value = tDBElement.Subtitles(i).SubsForced
                        SQLcommandMoviesSubs.ExecuteNonQuery()
                        iID += 1
                    Next
                End Using

                'MovieSets part
                Using SQLcommand_setlinkmovie As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_setlinkmovie.CommandText = String.Concat("DELETE FROM setlinkmovie WHERE idMovie = ", tDBElement.ID, ";")
                    SQLcommand_setlinkmovie.ExecuteNonQuery()
                End Using

                Dim IsNewSet As Boolean
                For Each s As MediaContainers.SetDetails In tDBElement.MainDetails.Sets
                    If s.TitleSpecified Then
                        IsNewSet = Not s.ID > 0
                        If Not IsNewSet Then
                            Using SQLcommand_setlinkmovie As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                SQLcommand_setlinkmovie.CommandText = String.Concat("INSERT OR REPLACE INTO setlinkmovie (",
                             "idMovie, idSet, iOrder",
                             ") VALUES (?,?,?);")
                                Dim par_setlinkmovie_idMovie As SQLiteParameter = SQLcommand_setlinkmovie.Parameters.Add("parSets_idMovie", DbType.Int64, 0, "idMovie")
                                Dim par_setlinkmovie_idSet As SQLiteParameter = SQLcommand_setlinkmovie.Parameters.Add("parSets_idSet", DbType.Int64, 0, "idSet")
                                Dim par_setlinkmovie_iOrder As SQLiteParameter = SQLcommand_setlinkmovie.Parameters.Add("parSets_iOrder", DbType.Int64, 0, "iOrder")

                                par_setlinkmovie_idMovie.Value = tDBElement.ID
                                par_setlinkmovie_idSet.Value = s.ID
                                par_setlinkmovie_iOrder.Value = s.Order
                                SQLcommand_setlinkmovie.ExecuteNonQuery()
                            End Using
                        Else
                            'first check if a Set with same TMDBColID is already existing
                            If s.TMDBSpecified Then
                                Using SQLcommand_sets As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                    SQLcommand_sets.CommandText = String.Concat("SELECT idSet, SetName, Plot ",
                                                                               "FROM sets WHERE TMDBColID LIKE """, s.TMDB, """;")
                                    Using SQLreader As SQLiteDataReader = SQLcommand_sets.ExecuteReader()
                                        If SQLreader.HasRows Then
                                            SQLreader.Read()
                                            If Not DBNull.Value.Equals(SQLreader("idSet")) Then s.ID = CInt(SQLreader("idSet"))
                                            If Not DBNull.Value.Equals(SQLreader("SetName")) Then s.Title = CStr(SQLreader("SetName"))
                                            If Not DBNull.Value.Equals(SQLreader("Plot")) Then s.Plot = CStr(SQLreader("Plot"))
                                            IsNewSet = False
                                            NFO.SaveToNFO_Movie(tDBElement, False) 'to save the "new" SetName
                                        Else
                                            IsNewSet = True
                                        End If
                                    End Using
                                End Using
                            End If

                            If IsNewSet Then
                                'secondly check if a Set with same name is already existing
                                Using SQLcommand_sets As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                    SQLcommand_sets.CommandText = String.Concat("SELECT idSet, Plot ",
                                                                               "FROM sets WHERE SetName LIKE """, s.Title, """;")
                                    Using SQLreader As SQLiteDataReader = SQLcommand_sets.ExecuteReader()
                                        If SQLreader.HasRows Then
                                            SQLreader.Read()
                                            If Not DBNull.Value.Equals(SQLreader("idSet")) Then s.ID = CInt(SQLreader("idSet"))
                                            If Not DBNull.Value.Equals(SQLreader("Plot")) Then s.Plot = CStr(SQLreader("Plot"))
                                            IsNewSet = False
                                            NFO.SaveToNFO_Movie(tDBElement, False) 'to save the "new" Plot
                                        Else
                                            IsNewSet = True
                                        End If
                                    End Using
                                End Using
                            End If

                            If Not IsNewSet Then
                                'create new setlinkmovie with existing SetID
                                Using SQLcommand_setlinkmovie As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                    SQLcommand_setlinkmovie.CommandText = String.Concat("INSERT OR REPLACE INTO setlinkmovie (",
                                                                                     "idMovie, idSet, iOrder",
                                                                                     ") VALUES (?,?,?);")
                                    Dim par_setlinkmovie_idMovie As SQLiteParameter = SQLcommand_setlinkmovie.Parameters.Add("parSets_idMovie", DbType.Int64, 0, "idMovie")
                                    Dim par_setlinkmovie_idSet As SQLiteParameter = SQLcommand_setlinkmovie.Parameters.Add("parSets_idSet", DbType.Int64, 0, "idSet")
                                    Dim par_setlinkmovie_iOrder As SQLiteParameter = SQLcommand_setlinkmovie.Parameters.Add("parSets_iOrder", DbType.Int64, 0, "iOrder")

                                    par_setlinkmovie_idMovie.Value = tDBElement.ID
                                    par_setlinkmovie_idSet.Value = s.ID
                                    par_setlinkmovie_iOrder.Value = s.Order
                                    SQLcommand_setlinkmovie.ExecuteNonQuery()
                                End Using

                                'update existing set with latest TMDB Collection ID
                                Using SQLcommand_sets As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                    SQLcommand_sets.CommandText = String.Format("UPDATE sets SET TMDBColID=? WHERE idSet={0}", s.ID)
                                    Dim par_sets_TMDBColID As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_TMDBColID", DbType.String, 0, "TMDBColID")
                                    par_sets_TMDBColID.Value = s.TMDB
                                    SQLcommand_sets.ExecuteNonQuery()
                                End Using
                            Else
                                'create new Set
                                Using SQLcommand_sets As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                    SQLcommand_sets.CommandText = String.Concat("INSERT OR REPLACE INTO sets (",
                                                                                     "ListTitle, NfoPath, TMDBColID, Plot, SetName, ",
                                                                                     "New, Mark, Lock, SortMethod, Language",
                                                                                     ") VALUES (?,?,?,?,?,?,?,?,?,?);")
                                    Dim par_sets_ListTitle As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_ListTitle", DbType.String, 0, "ListTitle")
                                    Dim par_sets_NfoPath As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_NfoPath", DbType.String, 0, "NfoPath")
                                    Dim par_sets_TMDBColID As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_TMDBColID", DbType.String, 0, "TMDBColID")
                                    Dim par_sets_Plot As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_Plot", DbType.String, 0, "Plot")
                                    Dim par_sets_SetName As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_SetName", DbType.String, 0, "SetName")
                                    Dim par_sets_New As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_New", DbType.Boolean, 0, "New")
                                    Dim par_sets_Mark As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_Mark", DbType.Boolean, 0, "Mark")
                                    Dim par_sets_Lock As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_Lock", DbType.Boolean, 0, "Lock")
                                    Dim par_sets_SortMethod As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_SortMethod", DbType.Int64, 0, "SortMethod")
                                    Dim par_sets_Language As SQLiteParameter = SQLcommand_sets.Parameters.Add("parSets_Language", DbType.String, 0, "Language")

                                    par_sets_SetName.Value = s.Title
                                    par_sets_ListTitle.Value = StringUtils.SortTokens_MovieSet(s.Title)
                                    par_sets_TMDBColID.Value = s.TMDB
                                    par_sets_Plot.Value = s.Plot
                                    par_sets_NfoPath.Value = String.Empty
                                    par_sets_New.Value = True
                                    par_sets_Lock.Value = False
                                    par_sets_SortMethod.Value = Enums.SortMethod_MovieSet.Year
                                    par_sets_Language.Value = tDBElement.Language

                                    If Master.eSettings.MovieSetGeneralMarkNew Then
                                        par_sets_Mark.Value = True
                                    Else
                                        par_sets_Mark.Value = False
                                    End If
                                    SQLcommand_sets.ExecuteNonQuery()
                                End Using

                                Using SQLcommand_sets As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                    SQLcommand_sets.CommandText = String.Concat("SELECT idSet, SetName FROM sets WHERE SetName LIKE """, s.Title, """;")
                                    Using rdrSets As SQLiteDataReader = SQLcommand_sets.ExecuteReader()
                                        If rdrSets.Read Then
                                            s.ID = Convert.ToInt64(rdrSets(0))
                                        End If
                                    End Using
                                End Using

                                'create new setlinkmovie with new SetID
                                If s.ID > 0 Then
                                    Using SQLcommand_setlinkmovie As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                        SQLcommand_setlinkmovie.CommandText = String.Concat("INSERT OR REPLACE INTO setlinkmovie (",
                                                                                         "idMovie, idSet, iOrder",
                                                                                         ") VALUES (?,?,?);")
                                        Dim par_setlinkmovie_idMovie As SQLiteParameter = SQLcommand_setlinkmovie.Parameters.Add("parSets_idMovie", DbType.Int64, 0, "idMovie")
                                        Dim par_setlinkmovie_idSet As SQLiteParameter = SQLcommand_setlinkmovie.Parameters.Add("parSets_idSet", DbType.Int64, 0, "idSet")
                                        Dim par_setlinkmovie_iOrder As SQLiteParameter = SQLcommand_setlinkmovie.Parameters.Add("parSets_iOrder", DbType.Int64, 0, "iOrder")

                                        par_setlinkmovie_idMovie.Value = tDBElement.ID
                                        par_setlinkmovie_idSet.Value = s.ID
                                        par_setlinkmovie_iOrder.Value = s.Order
                                        SQLcommand_setlinkmovie.ExecuteNonQuery()
                                    End Using
                                End If
                            End If
                        End If
                    End If
                Next
            End If
        End Using

        'YAMJ watched file
        If tDBElement.MainDetails.PlayCountSpecified AndAlso Master.eSettings.Movie.Filenaming.YAMJ.Enabled AndAlso Master.eSettings.Movie.Filenaming.YAMJ.WatchedFile Then
            For Each a In FileUtils.GetFilenameList.Movie(tDBElement, Enums.ScrapeModifierType.MainWatchedFile)
                If Not File.Exists(a) Then
                    Dim fs As FileStream = File.Create(a)
                    fs.Close()
                End If
            Next
        ElseIf Not tDBElement.MainDetails.PlayCountSpecified AndAlso Master.eSettings.Movie.Filenaming.YAMJ.Enabled AndAlso Master.eSettings.Movie.Filenaming.YAMJ.WatchedFile Then
            For Each a In FileUtils.GetFilenameList.Movie(tDBElement, Enums.ScrapeModifierType.MainWatchedFile)
                If File.Exists(a) Then
                    File.Delete(a)
                End If
            Next
        End If

        If Not bBatchMode Then SQLtransaction.Commit()

        If bDoSync Then
            AddonsManager.Instance.RunGeneric(Enums.AddonEventType.Sync_Movie, Nothing, Nothing, False, tDBElement)
        End If

        Return tDBElement
    End Function
    ''' <summary>
    ''' Saves all information from a Database.DBElement object to the database
    ''' </summary>
    ''' <param name="tDBElement">Media.Movie object to save to the database</param>
    ''' <param name="bBatchMode">Is the function already part of a transaction?</param>
    ''' <param name="bToDisk">Create NFO and Images</param>
    ''' <returns>Database.DBElement object</returns>
    Public Function Save_MovieSet(ByVal tDBElement As DBElement, ByVal bBatchMode As Boolean, ByVal bToNFO As Boolean, ByVal bToDisk As Boolean, ByVal bDoSync As Boolean) As DBElement
        If tDBElement.MainDetails Is Nothing Then Return tDBElement

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            If Not tDBElement.IDSpecified Then
                SQLcommand.CommandText = String.Concat("INSERT OR REPLACE INTO sets (",
                 "ListTitle, NfoPath, TMDBColID, Plot, SetName, New, Mark, Lock, SortMethod, Language, DateModified",
                 ") VALUES (?,?,?,?,?,?,?,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM sets;")
            Else
                SQLcommand.CommandText = String.Concat("INSERT OR REPLACE INTO sets (",
                 "idSet, ListTitle, NfoPath, TMDBColID, Plot, SetName, New, Mark, Lock, SortMethod, Language, DateModified",
                 ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM sets;")
                Dim parMovieSetID As SQLiteParameter = SQLcommand.Parameters.Add("parMovieSetID", DbType.Int64, 0, "idSet")
                parMovieSetID.Value = tDBElement.ID
            End If
            Dim parListTitle As SQLiteParameter = SQLcommand.Parameters.Add("parListTitle", DbType.String, 0, "ListTitle")
            Dim parNfoPath As SQLiteParameter = SQLcommand.Parameters.Add("parNfoPath", DbType.String, 0, "NfoPath")
            Dim parTMDBColID As SQLiteParameter = SQLcommand.Parameters.Add("parTMDBColID", DbType.Int32, 0, "TMDBColID")
            Dim parPlot As SQLiteParameter = SQLcommand.Parameters.Add("parPlot", DbType.String, 0, "Plot")
            Dim parSetName As SQLiteParameter = SQLcommand.Parameters.Add("parSetName", DbType.String, 0, "SetName")
            Dim parNew As SQLiteParameter = SQLcommand.Parameters.Add("parNew", DbType.Boolean, 0, "New")
            Dim parMark As SQLiteParameter = SQLcommand.Parameters.Add("parMark", DbType.Boolean, 0, "Mark")
            Dim parLock As SQLiteParameter = SQLcommand.Parameters.Add("parLock", DbType.Boolean, 0, "Lock")
            Dim parSortMethod As SQLiteParameter = SQLcommand.Parameters.Add("parSortMethod", DbType.Int16, 0, "SortMethod")
            Dim parLanguage As SQLiteParameter = SQLcommand.Parameters.Add("parLanguage", DbType.String, 0, "Language")
            Dim parDateModified As SQLiteParameter = SQLcommand.Parameters.Add("parDateModified", DbType.Int64, 0, "DateModified")

            'DateModified
            Try
                If Not tDBElement.IDSpecified AndAlso tDBElement.MainDetails.DateModifiedSpecified Then
                    Dim DateTimeDateModified As Date = Date.ParseExact(tDBElement.MainDetails.DateModified, "yyyy-MM-dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
                    parDateModified.Value = Functions.ConvertToUnixTimestamp(DateTimeDateModified)
                ElseIf tDBElement.IDSpecified Then
                    parDateModified.Value = Functions.ConvertToUnixTimestamp(Date.Now)
                End If
                If parDateModified.Value IsNot Nothing Then
                    tDBElement.MainDetails.DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(parDateModified.Value)).ToString("yyyy-MM-dd HH:mm:ss")
                Else
                    tDBElement.MainDetails.DateModified = String.Empty
                End If
            Catch
                parDateModified.Value = If(Not tDBElement.IDSpecified, Functions.ConvertToUnixTimestamp(Date.Now), tDBElement.DateModified)
                tDBElement.MainDetails.DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(parDateModified.Value)).ToString("yyyy-MM-dd HH:mm:ss")
            End Try

            'First let's save it to NFO, even because we will need the NFO path, also save Images
            'art Table be be linked later
            If bToNFO Then NFO.SaveToNFO_MovieSet(tDBElement)
            If bToDisk Then
                tDBElement.ImagesContainer.SaveAllImages(tDBElement, False)
            End If

            parNfoPath.Value = tDBElement.NfoPath
            parLanguage.Value = tDBElement.Language

            parNew.Value = Not tDBElement.IDSpecified
            parMark.Value = tDBElement.IsMark
            parLock.Value = tDBElement.IsLock
            parSortMethod.Value = tDBElement.SortMethod

            parListTitle.Value = tDBElement.ListTitle
            parSetName.Value = tDBElement.MainDetails.Title
            If tDBElement.MainDetails.TMDBColIDSpecified Then 'need to be NOTHING instead of "0"
                parTMDBColID.Value = tDBElement.MainDetails.TMDB
            End If
            parPlot.Value = tDBElement.MainDetails.Plot

            If Not tDBElement.IDSpecified Then
                If Master.eSettings.MovieSetGeneralMarkNew Then
                    parMark.Value = True
                    tDBElement.IsMark = True
                End If
                Using rdrMovieSet As SQLiteDataReader = SQLcommand.ExecuteReader()
                    If rdrMovieSet.Read Then
                        tDBElement.ID = Convert.ToInt64(rdrMovieSet(0))
                    Else
                        logger.Error("Something very wrong here: SaveMovieSetToDB", tDBElement.ToString, "Error")
                        tDBElement.ListTitle = "SETERROR"
                        Return tDBElement
                    End If
                End Using
            Else
                SQLcommand.ExecuteNonQuery()
            End If
        End Using

        'Images
        Using SQLcommand_art As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand_art.CommandText = String.Concat("DELETE FROM art WHERE media_id = ", tDBElement.ID, " AND media_type = 'set';")
            SQLcommand_art.ExecuteNonQuery()
        End Using
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Banner.LocalFilePath) Then SetArtForItem(tDBElement.ID, "set", "banner", tDBElement.ImagesContainer.Banner.LocalFilePath)
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.ClearArt.LocalFilePath) Then SetArtForItem(tDBElement.ID, "set", "clearart", tDBElement.ImagesContainer.ClearArt.LocalFilePath)
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.ClearLogo.LocalFilePath) Then SetArtForItem(tDBElement.ID, "set", "clearlogo", tDBElement.ImagesContainer.ClearLogo.LocalFilePath)
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.DiscArt.LocalFilePath) Then SetArtForItem(tDBElement.ID, "set", "discart", tDBElement.ImagesContainer.DiscArt.LocalFilePath)
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Fanart.LocalFilePath) Then SetArtForItem(tDBElement.ID, "set", "fanart", tDBElement.ImagesContainer.Fanart.LocalFilePath)
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Landscape.LocalFilePath) Then SetArtForItem(tDBElement.ID, "set", "landscape", tDBElement.ImagesContainer.Landscape.LocalFilePath)
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Poster.LocalFilePath) Then SetArtForItem(tDBElement.ID, "set", "poster", tDBElement.ImagesContainer.Poster.LocalFilePath)

        'save set informations to movies
        For Each tMovie In tDBElement.MoviesInSet
            tMovie.DBMovie.MainDetails.AddSet(New MediaContainers.SetDetails With {
                                                .ID = tDBElement.ID,
                                                .Order = tMovie.Order,
                                                .Plot = tDBElement.MainDetails.Plot,
                                                .Title = tDBElement.MainDetails.Title,
                                                .TMDB = tDBElement.MainDetails.TMDB})
            AddonsManager.Instance.RunGeneric(Enums.AddonEventType.BeforeEdit_Movie, Nothing, Nothing, False, tMovie.DBMovie)
            AddonsManager.Instance.RunGeneric(Enums.AddonEventType.AfterEdit_Movie, Nothing, Nothing, False, tMovie.DBMovie)
            Save_Movie(tMovie.DBMovie, True, True, False, True, False)
            RaiseEvent GenericEvent(Enums.AddonEventType.AfterEdit_Movie, New List(Of Object)(New Object() {tMovie.DBMovie.ID}))
        Next

        'remove set-information from movies which are no longer assigned to this set
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand.CommandText = String.Concat("SELECT idMovie, idSet FROM setlinkmovie ",
                                                       "WHERE idSet = ", tDBElement.ID, ";")
            Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                While SQLreader.Read
                    Dim rMovie = tDBElement.MoviesInSet.FirstOrDefault(Function(f) f.DBMovie.ID = Convert.ToInt64(SQLreader("idMovie")))
                    If rMovie Is Nothing Then
                        'movie is no longer a part of this set
                        Dim tMovie As Database.DBElement = Load_Movie(Convert.ToInt64(SQLreader("idMovie")))
                        tMovie.MainDetails.RemoveSet(tDBElement.ID)
                        AddonsManager.Instance.RunGeneric(Enums.AddonEventType.BeforeEdit_Movie, Nothing, Nothing, False, tMovie)
                        AddonsManager.Instance.RunGeneric(Enums.AddonEventType.AfterEdit_Movie, Nothing, Nothing, False, tMovie)
                        Save_Movie(tMovie, True, True, False, True, False)
                        RaiseEvent GenericEvent(Enums.AddonEventType.AfterEdit_Movie, New List(Of Object)(New Object() {tMovie.ID}))
                    End If
                End While
            End Using
        End Using

        If Not bBatchMode Then SQLtransaction.Commit()

        AddonsManager.Instance.RunGeneric(Enums.AddonEventType.Sync_Movieset, Nothing, Nothing, False, tDBElement)

        Return tDBElement
    End Function

    ''' <summary>
    ''' Saves all information from a Database.DBElement object to the database
    ''' </summary>
    ''' <param name="tDBTag">Media.Movie object to save to the database</param>
    ''' <param name="bIsNew">Is this a new movieset (not already present in database)?</param>
    ''' <param name="bBatchMode">Is the function already part of a transaction?</param>
    ''' <param name="bToNfo">Save the information to an nfo file?</param>
    ''' <param name="bWithMovies">Save the information also to all linked movies?</param>
    ''' <returns>Database.DBElement object</returns>
    Public Function Save_Tag_Movie(ByVal tDBTag As Structures.DBMovieTag, ByVal bIsNew As Boolean, ByVal bBatchMode As Boolean, ByVal bToNfo As Boolean, ByVal bWithMovies As Boolean) As Structures.DBMovieTag
        If tDBTag.ID = -1 Then bIsNew = True

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            If bIsNew Then
                SQLcommand.CommandText = String.Concat("INSERT OR REPLACE INTO tag (strTag) VALUES (?); SELECT LAST_INSERT_ROWID() FROM tag;")
            Else
                SQLcommand.CommandText = String.Concat("INSERT OR REPLACE INTO tag (",
                          "idTag, strTag) VALUES (?,?); SELECT LAST_INSERT_ROWID() FROM tag;")
                Dim parTagID As SQLiteParameter = SQLcommand.Parameters.Add("parTagID", DbType.Int64, 0, "idTag")
                parTagID.Value = tDBTag.ID
            End If
            Dim parTitle As SQLiteParameter = SQLcommand.Parameters.Add("parTitle", DbType.String, 0, "strTag")

            parTitle.Value = tDBTag.Title

            If bIsNew Then
                Using rdrMovieTag As SQLiteDataReader = SQLcommand.ExecuteReader()
                    If rdrMovieTag.Read Then
                        tDBTag.ID = CInt(Convert.ToInt64(rdrMovieTag(0)))
                    Else
                        logger.Error("Something very wrong here: SaveMovieSetToDB", tDBTag.ToString, "Error")
                        tDBTag.Title = "SETERROR"
                        Return tDBTag
                    End If
                End Using
            Else
                SQLcommand.ExecuteNonQuery()
            End If
        End Using


        If bWithMovies Then
            'Update all movies for this tag: if there are movies in linktag-table which aren't in current tag.movies object then remove movie-tag link from linktable and nfo for those movies

            'old state of tag in database
            Dim MoviesInTagOld As New List(Of DBElement)
            'new/updatend state of tag
            Dim MoviesInTagNew As New List(Of DBElement)
            MoviesInTagNew.AddRange(tDBTag.Movies.ToArray)





            'get all movies linked to this tag from database (old state)
            Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand.CommandText = String.Concat("SELECT idMedia, idTag FROM taglinks ",
                   "WHERE idTag = ", tDBTag.ID, " AND media_type = 'movie';")

                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                    While SQLreader.Read
                        If Not DBNull.Value.Equals(SQLreader("idMedia")) Then
                            MoviesInTagOld.Add(Load_Movie(Convert.ToInt64(SQLreader("idMedia"))))
                        End If
                    End While
                End Using
            End Using

            'check if there are movies in linktable which aren't in current tag - those are old entries which meed to be removed from linktag table and nfo of movies
            For i = MoviesInTagOld.Count - 1 To 0 Step -1
                For Each movienew In MoviesInTagNew
                    If MoviesInTagOld(i).MainDetails.IMDB = movienew.MainDetails.IMDB Then
                        MoviesInTagOld.RemoveAt(i)
                        Exit For
                    End If
                Next
            Next

            'write tag information into nfo (add tag)
            If MoviesInTagNew.Count > 0 Then
                For Each tMovie In MoviesInTagNew
                    Dim mMovie As DBElement = Load_Movie(tMovie.ID) 'TODO: check why we load mMovie to overwrite tMovie with himself
                    tMovie = mMovie
                    mMovie.MainDetails.AddTag(tDBTag.Title)
                    Master.DB.Save_Movie(mMovie, bBatchMode, True, False, True, False)
                Next
            End If
            'clean nfo of movies who aren't part of tag anymore (remove tag)
            If MoviesInTagOld.Count > 0 Then
                For Each tMovie In MoviesInTagOld
                    Dim mMovie As DBElement = Load_Movie(tMovie.ID) 'TODO: check why we load mMovie to overwrite tMovie with himself
                    tMovie = mMovie
                    mMovie.MainDetails.Tags.Remove(tDBTag.Title)
                    Master.DB.Save_Movie(mMovie, bBatchMode, True, False, True, False)
                Next
            End If
        End If

        If Not bBatchMode Then SQLtransaction.Commit()

        Return tDBTag
    End Function

    Public Sub Change_TVEpisode(ByVal tDBElement As DBElement, ByVal lstListOfEpisodes As List(Of MediaContainers.MainDetails), Optional ByVal bBatchMode As Boolean = False)
        Dim newEpisodesList As New List(Of DBElement)

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
        Using SQLPCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()

            'first step: remove all existing episode informations for this file and set it to "Missing"
            Delete_TVEpisode(tDBElement.FileItem.FullPath, False, True)

            'second step: create new episode DBElements and save it to database
            For Each tEpisode As MediaContainers.MainDetails In lstListOfEpisodes
                Dim newEpisode As New DBElement(Enums.ContentType.TVEpisode)
                newEpisode = New DBElement(Enums.ContentType.TVEpisode)
                newEpisode = CType(tDBElement.CloneDeep, DBElement)
                newEpisode.FileID = -1
                newEpisode.ID = -1
                newEpisode.MainDetails = tEpisode
                newEpisode.MainDetails.FileInfo = tDBElement.MainDetails.FileInfo
                Save_TVEpisode(newEpisode, True, True, True, True, False)
                newEpisodesList.Add(newEpisode)
            Next

            For Each tEpisode As DBElement In newEpisodesList
                AddonsManager.Instance.RunGeneric(Enums.AddonEventType.DuringUpdateDB_TV, Nothing, Nothing, False, tEpisode)
            Next

            For Each tEpisode As DBElement In newEpisodesList
                Save_TVEpisode(tEpisode, True, False, False, False, True, True)
            Next
        End Using
        If Not bBatchMode Then SQLtransaction.Commit()
    End Sub
    ''' <summary>
    ''' Saves all episode information from a Database.DBElement object to the database
    ''' </summary>
    ''' <param name="tDBElement">Database.DBElement object to save to the database</param>
    ''' <param name="bDoSeasonCheck">If <c>True</c> then check if it's needed to create a new season for this episode</param>
    ''' <param name="bBatchMode">Is the function already part of a transaction?</param>
    ''' <param name="bToDisk">Create NFO and Images</param>
    Public Function Save_TVEpisode(ByVal tDBElement As DBElement, ByVal bBatchMode As Boolean, ByVal bToNFO As Boolean, ByVal bToDisk As Boolean, ByVal bDoSeasonCheck As Boolean, ByVal bDoSync As Boolean, Optional ByVal bForceIsNewFlag As Boolean = False) As DBElement
        If tDBElement.MainDetails Is Nothing Then Return tDBElement

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        'delete so it will remove if there is a "missing" episode entry already. Only "missing" episodes must be deleted.
        Using SQLCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLCommand.CommandText = String.Concat("DELETE FROM episode WHERE idShow = ", tDBElement.ShowID, " AND Episode = ", tDBElement.MainDetails.Episode, " AND Season = ", tDBElement.MainDetails.Season, " AND idFile = -1;")
            SQLCommand.ExecuteNonQuery()
        End Using

        If tDBElement.FilenameSpecified Then
            If tDBElement.FileIDSpecified Then
                Using SQLpathcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLpathcommand.CommandText = String.Concat("INSERT OR REPLACE INTO files (idFile, strFilename) VALUES (?,?);")

                    Dim parID As SQLiteParameter = SQLpathcommand.Parameters.Add("parFileID", DbType.Int64, 0, "idFile")
                    Dim parFilename As SQLiteParameter = SQLpathcommand.Parameters.Add("parFilename", DbType.String, 0, "strFilename")
                    parID.Value = tDBElement.FileID
                    parFilename.Value = tDBElement.FileItem.FullPath
                    SQLpathcommand.ExecuteNonQuery()
                End Using
            Else
                Using SQLpathcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLpathcommand.CommandText = "SELECT idFile FROM files WHERE strFilename = (?);"

                    Dim parPath As SQLiteParameter = SQLpathcommand.Parameters.Add("parFilename", DbType.String, 0, "strFilename")
                    parPath.Value = tDBElement.FileItem.FullPath

                    Using SQLreader As SQLiteDataReader = SQLpathcommand.ExecuteReader
                        If SQLreader.HasRows Then
                            SQLreader.Read()
                            tDBElement.FileID = Convert.ToInt64(SQLreader("idFile"))
                        Else
                            Using SQLpcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                                SQLpcommand.CommandText = String.Concat("INSERT INTO files (",
                                     "strFilename) VALUES (?); SELECT LAST_INSERT_ROWID() FROM files;")
                                Dim parEpPath As SQLiteParameter = SQLpcommand.Parameters.Add("parEpPath", DbType.String, 0, "strFilename")
                                parEpPath.Value = tDBElement.FileItem.FullPath

                                tDBElement.FileID = Convert.ToInt64(SQLpcommand.ExecuteScalar)
                            End Using
                        End If
                    End Using
                End Using
            End If
        End If

        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            If Not tDBElement.IDSpecified Then
                SQLcommand.CommandText = String.Concat("INSERT OR REPLACE INTO episode (",
                 "idShow, idFile, idSource, New, Mark, Lock, Title, Season, Episode, ",
                 "Rating, Plot, Aired, NfoPath, Playcount, ",
                 "DisplaySeason, DisplayEpisode, DateAdded, Runtime, Votes, VideoSource, HasSub, SubEpisode, ",
                 "LastPlayed, IMDB, TMDB, TVDB, UserRating, DateModified",
                 ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM episode;")

            Else
                SQLcommand.CommandText = String.Concat("INSERT OR REPLACE INTO episode (",
                 "idEpisode, idShow, idFile, idSource, New, Mark, Lock, Title, Season, Episode, ",
                 "Rating, Plot, Aired, NfoPath, Playcount, ",
                 "DisplaySeason, DisplayEpisode, DateAdded, Runtime, Votes, VideoSource, HasSub, SubEpisode, ",
                 "LastPlayed, IMDB, TMDB, TVDB, UserRating, DateModified",
                 ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM episode;")

                Dim parTVEpisodeID As SQLiteParameter = SQLcommand.Parameters.Add("parTVEpisodeID", DbType.Int64, 0, "idEpisode")
                parTVEpisodeID.Value = tDBElement.ID
            End If

            Dim parTVShowID As SQLiteParameter = SQLcommand.Parameters.Add("parTVShowID", DbType.Int64, 0, "idShow")
            Dim parTVFileID As SQLiteParameter = SQLcommand.Parameters.Add("parTVFileID", DbType.Int64, 0, "idFile")
            Dim parSourceID As SQLiteParameter = SQLcommand.Parameters.Add("parSourceID", DbType.Int64, 0, "idSource")
            Dim parNew As SQLiteParameter = SQLcommand.Parameters.Add("parNew", DbType.Boolean, 0, "new")
            Dim parMark As SQLiteParameter = SQLcommand.Parameters.Add("parMark", DbType.Boolean, 0, "mark")
            Dim parLock As SQLiteParameter = SQLcommand.Parameters.Add("parLock", DbType.Boolean, 0, "lock")
            Dim parTitle As SQLiteParameter = SQLcommand.Parameters.Add("parTitle", DbType.String, 0, "Title")
            Dim parSeason As SQLiteParameter = SQLcommand.Parameters.Add("parSeason", DbType.String, 0, "Season")
            Dim parEpisode As SQLiteParameter = SQLcommand.Parameters.Add("parEpisode", DbType.String, 0, "Episode")
            Dim parRating As SQLiteParameter = SQLcommand.Parameters.Add("parRating", DbType.String, 0, "Rating")
            Dim parPlot As SQLiteParameter = SQLcommand.Parameters.Add("parPlot", DbType.String, 0, "Plot")
            Dim parAired As SQLiteParameter = SQLcommand.Parameters.Add("parAired", DbType.String, 0, "Aired")
            Dim parNfoPath As SQLiteParameter = SQLcommand.Parameters.Add("parNfoPath", DbType.String, 0, "NfoPath")
            Dim parPlaycount As SQLiteParameter = SQLcommand.Parameters.Add("parPlaycount", DbType.Int64, 0, "Playcount")
            Dim parDisplaySeason As SQLiteParameter = SQLcommand.Parameters.Add("parDisplaySeason", DbType.String, 0, "DisplaySeason")
            Dim parDisplayEpisode As SQLiteParameter = SQLcommand.Parameters.Add("parDisplayEpisode", DbType.String, 0, "DisplayEpisode")
            Dim parDateAdded As SQLiteParameter = SQLcommand.Parameters.Add("parDateAdded", DbType.Int64, 0, "DateAdded")
            Dim parRuntime As SQLiteParameter = SQLcommand.Parameters.Add("parRuntime", DbType.String, 0, "Runtime")
            Dim parVotes As SQLiteParameter = SQLcommand.Parameters.Add("parVotes", DbType.String, 0, "Votes")
            Dim parVideoSource As SQLiteParameter = SQLcommand.Parameters.Add("parVideoSource", DbType.String, 0, "VideoSource")
            Dim parHasSub As SQLiteParameter = SQLcommand.Parameters.Add("parHasSub", DbType.Boolean, 0, "HasSub")
            Dim parSubEpisode As SQLiteParameter = SQLcommand.Parameters.Add("parSubEpisode", DbType.String, 0, "SubEpisode")
            Dim par_LastPlayed As SQLiteParameter = SQLcommand.Parameters.Add("par_iLastPlayed", DbType.Int64, 0, "LastPlayed")
            Dim par_IMDB As SQLiteParameter = SQLcommand.Parameters.Add("par_IMDB", DbType.String, 0, "IMDB")
            Dim par_TMDB As SQLiteParameter = SQLcommand.Parameters.Add("par_TMDB", DbType.Int32, 0, "TMDB")
            Dim par_TVDB As SQLiteParameter = SQLcommand.Parameters.Add("par_TVDB", DbType.Int32, 0, "TVDB")
            Dim par_UserRating As SQLiteParameter = SQLcommand.Parameters.Add("par_UserRating", DbType.Int64, 0, "UserRating")
            Dim parDateModified As SQLiteParameter = SQLcommand.Parameters.Add("parDateModified", DbType.Int64, 0, "DateModified")

            'DateAdded
            Try
                If Not Master.eSettings.GeneralDateAddedIgnoreNFO AndAlso Not String.IsNullOrEmpty(tDBElement.MainDetails.DateAdded) Then
                    Dim DateTimeAdded As Date = Date.ParseExact(tDBElement.MainDetails.DateAdded, "yyyy-MM-dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
                    parDateAdded.Value = Functions.ConvertToUnixTimestamp(DateTimeAdded)
                Else
                    Select Case Master.eSettings.GeneralDateTime
                        Case Enums.DateTime.Now
                            parDateAdded.Value = If(Not tDBElement.IDSpecified, Functions.ConvertToUnixTimestamp(Date.Now), tDBElement.DateAdded)
                        Case Enums.DateTime.ctime
                            Dim ctime As Date = File.GetCreationTime(tDBElement.FileItem.FirstStackedPath)
                            If ctime.Year > 1601 Then
                                parDateAdded.Value = Functions.ConvertToUnixTimestamp(ctime)
                            Else
                                Dim mtime As Date = File.GetLastWriteTime(tDBElement.FileItem.FirstStackedPath)
                                parDateAdded.Value = Functions.ConvertToUnixTimestamp(mtime)
                            End If
                        Case Enums.DateTime.mtime
                            Dim mtime As Date = File.GetLastWriteTime(tDBElement.FileItem.FirstStackedPath)
                            If mtime.Year > 1601 Then
                                parDateAdded.Value = Functions.ConvertToUnixTimestamp(mtime)
                            Else
                                Dim ctime As Date = File.GetCreationTime(tDBElement.FileItem.FirstStackedPath)
                                parDateAdded.Value = Functions.ConvertToUnixTimestamp(ctime)
                            End If
                        Case Enums.DateTime.Newer
                            Dim mtime As Date = File.GetLastWriteTime(tDBElement.FileItem.FirstStackedPath)
                            Dim ctime As Date = File.GetCreationTime(tDBElement.FileItem.FirstStackedPath)
                            If mtime > ctime Then
                                parDateAdded.Value = Functions.ConvertToUnixTimestamp(mtime)
                            Else
                                parDateAdded.Value = Functions.ConvertToUnixTimestamp(ctime)
                            End If
                    End Select
                End If
                tDBElement.MainDetails.DateAdded = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(parDateAdded.Value)).ToString("yyyy-MM-dd HH:mm:ss")
            Catch ex As Exception
                parDateAdded.Value = If(Not tDBElement.IDSpecified, Functions.ConvertToUnixTimestamp(Date.Now), tDBElement.DateAdded)
                tDBElement.MainDetails.DateAdded = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(parDateAdded.Value)).ToString("yyyy-MM-dd HH:mm:ss")
            End Try

            'DateModified
            Try
                If Not tDBElement.IDSpecified AndAlso tDBElement.MainDetails.DateModifiedSpecified Then
                    Dim DateTimeDateModified As Date = Date.ParseExact(tDBElement.MainDetails.DateModified, "yyyy-MM-dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
                    parDateModified.Value = Functions.ConvertToUnixTimestamp(DateTimeDateModified)
                ElseIf tDBElement.IDSpecified Then
                    parDateModified.Value = Functions.ConvertToUnixTimestamp(Date.Now)
                End If
                If parDateModified.Value IsNot Nothing Then
                    tDBElement.MainDetails.DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(parDateModified.Value)).ToString("yyyy-MM-dd HH:mm:ss")
                Else
                    tDBElement.MainDetails.DateModified = String.Empty
                End If
            Catch
                parDateModified.Value = If(Not tDBElement.IDSpecified, Functions.ConvertToUnixTimestamp(Date.Now), tDBElement.DateModified)
                tDBElement.MainDetails.DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(parDateModified.Value)).ToString("yyyy-MM-dd HH:mm:ss")
            End Try

            'LastPlayed
            Dim DateTimeLastPlayedUnix As Double = -1
            If tDBElement.MainDetails.LastPlayedSpecified Then
                Try
                    Dim DateTimeLastPlayed As Date = Date.ParseExact(tDBElement.MainDetails.LastPlayed, "yyyy-MM-dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
                    DateTimeLastPlayedUnix = Functions.ConvertToUnixTimestamp(DateTimeLastPlayed)
                Catch
                    'Kodi save it only as yyyy-MM-dd, try that
                    Try
                        Dim DateTimeLastPlayed As Date = Date.ParseExact(tDBElement.MainDetails.LastPlayed, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
                        DateTimeLastPlayedUnix = Functions.ConvertToUnixTimestamp(DateTimeLastPlayed)
                    Catch
                        DateTimeLastPlayedUnix = -1
                    End Try
                End Try
            End If
            If DateTimeLastPlayedUnix >= 0 Then
                par_LastPlayed.Value = DateTimeLastPlayedUnix
            Else
                par_LastPlayed.Value = Nothing 'need to be NOTHING instead of 0
                tDBElement.MainDetails.LastPlayed = String.Empty
            End If

            'First let's save it to NFO, even because we will need the NFO path, also save Images
            'art Table be be linked later
            If tDBElement.FileIDSpecified Then
                If bToNFO Then NFO.SaveToNFO_TVEpisode(tDBElement)
                If bToDisk Then
                    tDBElement.ImagesContainer.SaveAllImages(tDBElement, False)
                    tDBElement.MainDetails.SaveAllActorThumbs(tDBElement)
                End If
            End If

            parTVShowID.Value = tDBElement.ShowID
            parNfoPath.Value = tDBElement.NfoPath
            parHasSub.Value = (tDBElement.Subtitles IsNot Nothing AndAlso tDBElement.Subtitles.Count > 0) OrElse tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle.Count > 0
            parNew.Value = bForceIsNewFlag OrElse Not tDBElement.IDSpecified
            parMark.Value = tDBElement.IsMark
            parTVFileID.Value = tDBElement.FileID
            parLock.Value = tDBElement.IsLock
            parSourceID.Value = tDBElement.Source.ID
            parVideoSource.Value = tDBElement.VideoSource

            With tDBElement.MainDetails
                parTitle.Value = .Title
                parSeason.Value = .Season
                parEpisode.Value = .Episode
                parDisplaySeason.Value = .DisplaySeason
                parDisplayEpisode.Value = .DisplayEpisode
                par_UserRating.Value = .UserRating
                parRating.Value = .Rating
                parPlot.Value = .Plot
                parAired.Value = NumUtils.DateToISO8601Date(.Aired)
                If .PlayCountSpecified Then 'need to be NOTHING instead of "0"
                    parPlaycount.Value = .PlayCount
                End If
                parRuntime.Value = .Runtime
                parVotes.Value = .Votes
                If .SubEpisodeSpecified Then
                    parSubEpisode.Value = .SubEpisode
                End If
                par_IMDB.Value = .IMDB
                If .TMDBSpecified Then 'need to be NOTHING instead of "0"
                    par_TMDB.Value = .TMDB
                End If
                If .TVDBSpecified Then 'need to be NOTHING instead of "0"
                    par_TVDB.Value = .TVDB
                End If
            End With

            If Not tDBElement.IDSpecified Then
                If Master.eSettings.TVGeneralMarkNewEpisodes Then
                    parMark.Value = True
                    tDBElement.IsMark = True
                End If
                Using rdrTVEp As SQLiteDataReader = SQLcommand.ExecuteReader()
                    If rdrTVEp.Read Then
                        tDBElement.ID = Convert.ToInt64(rdrTVEp(0))
                    Else
                        logger.Error("Something very wrong here: SaveTVEpToDB", tDBElement.ToString, "Error")
                        tDBElement.ID = -1
                        Return tDBElement
                        Exit Function
                    End If
                End Using
            Else
                SQLcommand.ExecuteNonQuery()
            End If

            If tDBElement.IDSpecified Then

                'Actors
                Using SQLcommand_actorlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_actorlink.CommandText = String.Concat("DELETE FROM actorlinkepisode WHERE idEpisode = ", tDBElement.ID, ";")
                    SQLcommand_actorlink.ExecuteNonQuery()
                End Using
                AddCast(tDBElement.ID, "episode", "episode", tDBElement.MainDetails.Actors)

                'Directors
                Using SQLcommand_directorlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_directorlink.CommandText = String.Format("DELETE FROM directorlinkepisode WHERE idEpisode = {0};", tDBElement.ID)
                    SQLcommand_directorlink.ExecuteNonQuery()
                End Using
                For Each director As String In tDBElement.MainDetails.Directors
                    AddDirectorToEpisode(tDBElement.ID, AddActor(director, "", "", "", -1, False))
                Next

                'Guest Stars
                Using SQLcommand_gueststarlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_gueststarlink.CommandText = String.Concat("DELETE FROM gueststarlinkepisode WHERE idEpisode = ", tDBElement.ID, ";")
                    SQLcommand_gueststarlink.ExecuteNonQuery()
                End Using
                AddGuestStar(tDBElement.ID, "episode", "episode", tDBElement.MainDetails.GuestStars)

                'Images
                Using SQLcommand_art As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_art.CommandText = String.Concat("DELETE FROM art WHERE media_id = ", tDBElement.ID, " AND media_type = 'episode';")
                    SQLcommand_art.ExecuteNonQuery()
                End Using
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Fanart.LocalFilePath) Then SetArtForItem(tDBElement.ID, "episode", "fanart", tDBElement.ImagesContainer.Fanart.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Poster.LocalFilePath) Then SetArtForItem(tDBElement.ID, "episode", "thumb", tDBElement.ImagesContainer.Poster.LocalFilePath)

                'Writers
                Using SQLcommand_writerlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_writerlink.CommandText = String.Concat("DELETE FROM writerlinkepisode WHERE idEpisode = ", tDBElement.ID, ";")
                    SQLcommand_writerlink.ExecuteNonQuery()
                End Using
                For Each writer As String In tDBElement.MainDetails.Credits
                    AddWriterToEpisode(tDBElement.ID, AddActor(writer, "", "", "", -1, False))
                Next

                Using SQLcommandTVVStreams As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommandTVVStreams.CommandText = String.Concat("DELETE FROM TVVStreams WHERE TVEpID = ", tDBElement.ID, ";")
                    SQLcommandTVVStreams.ExecuteNonQuery()
                    SQLcommandTVVStreams.CommandText = String.Concat("INSERT OR REPLACE INTO TVVStreams (",
                       "TVEpID, StreamID, Video_Width, Video_Height, Video_Codec, Video_Duration, Video_ScanType, Video_AspectDisplayRatio,",
                       "Video_Language, Video_LongLanguage, Video_Bitrate, Video_MultiViewCount, Video_FileSize, Video_MultiViewLayout, ",
                       "Video_StereoMode) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);")

                    Dim parVideo_EpID As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_EpID", DbType.Int64, 0, "TVEpID")
                    Dim parVideo_StreamID As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_StreamID", DbType.Int64, 0, "StreamID")
                    Dim parVideo_Width As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_Width", DbType.String, 0, "Video_Width")
                    Dim parVideo_Height As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_Height", DbType.String, 0, "Video_Height")
                    Dim parVideo_Codec As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_Codec", DbType.String, 0, "Video_Codec")
                    Dim parVideo_Duration As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_Duration", DbType.String, 0, "Video_Duration")
                    Dim parVideo_ScanType As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_ScanType", DbType.String, 0, "Video_ScanType")
                    Dim parVideo_AspectDisplayRatio As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_AspectDisplayRatio", DbType.String, 0, "Video_AspectDisplayRatio")
                    Dim parVideo_Language As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_Language", DbType.String, 0, "Video_Language")
                    Dim parVideo_LongLanguage As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_LongLanguage", DbType.String, 0, "Video_LongLanguage")
                    Dim parVideo_Bitrate As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_Bitrate", DbType.String, 0, "Video_Bitrate")
                    Dim parVideo_MultiViewCount As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_MultiViewCount", DbType.String, 0, "Video_MultiViewCount")
                    Dim parVideo_FileSize As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_FileSize", DbType.Int64, 0, "Video_FileSize")
                    Dim parVideo_MultiViewLayout As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_MultiViewLayout", DbType.String, 0, "Video_MultiViewLayout")
                    Dim parVideo_StereoMode As SQLiteParameter = SQLcommandTVVStreams.Parameters.Add("parVideo_StereoMode", DbType.String, 0, "Video_StereoMode")

                    For i As Integer = 0 To tDBElement.MainDetails.FileInfo.StreamDetails.Video.Count - 1
                        parVideo_EpID.Value = tDBElement.ID
                        parVideo_StreamID.Value = i
                        parVideo_Width.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Width
                        parVideo_Height.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Height
                        parVideo_Codec.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Codec
                        parVideo_Duration.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Duration
                        parVideo_ScanType.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Scantype
                        parVideo_AspectDisplayRatio.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Aspect
                        parVideo_Language.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Language
                        parVideo_LongLanguage.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).LongLanguage
                        parVideo_Bitrate.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Bitrate
                        parVideo_MultiViewCount.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).MultiViewCount
                        parVideo_MultiViewLayout.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).MultiViewLayout
                        parVideo_FileSize.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).Filesize
                        parVideo_StereoMode.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Video(i).StereoMode

                        SQLcommandTVVStreams.ExecuteNonQuery()
                    Next
                End Using
                Using SQLcommandTVAStreams As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommandTVAStreams.CommandText = String.Concat("DELETE FROM TVAStreams WHERE TVEpID = ", tDBElement.ID, ";")
                    SQLcommandTVAStreams.ExecuteNonQuery()
                    SQLcommandTVAStreams.CommandText = String.Concat("INSERT OR REPLACE INTO TVAStreams (",
                       "TVEpID, StreamID, Audio_Language, Audio_LongLanguage, Audio_Codec, Audio_Channel, Audio_Bitrate",
                       ") VALUES (?,?,?,?,?,?,?);")

                    Dim parAudio_EpID As SQLiteParameter = SQLcommandTVAStreams.Parameters.Add("parAudio_EpID", DbType.Int64, 0, "TVEpID")
                    Dim parAudio_StreamID As SQLiteParameter = SQLcommandTVAStreams.Parameters.Add("parAudio_StreamID", DbType.Int64, 0, "StreamID")
                    Dim parAudio_Language As SQLiteParameter = SQLcommandTVAStreams.Parameters.Add("parAudio_Language", DbType.String, 0, "Audio_Language")
                    Dim parAudio_LongLanguage As SQLiteParameter = SQLcommandTVAStreams.Parameters.Add("parAudio_LongLanguage", DbType.String, 0, "Audio_LongLanguage")
                    Dim parAudio_Codec As SQLiteParameter = SQLcommandTVAStreams.Parameters.Add("parAudio_Codec", DbType.String, 0, "Audio_Codec")
                    Dim parAudio_Channel As SQLiteParameter = SQLcommandTVAStreams.Parameters.Add("parAudio_Channel", DbType.String, 0, "Audio_Channel")
                    Dim parAudio_Bitrate As SQLiteParameter = SQLcommandTVAStreams.Parameters.Add("parAudio_Bitrate", DbType.String, 0, "Audio_Bitrate")

                    For i As Integer = 0 To tDBElement.MainDetails.FileInfo.StreamDetails.Audio.Count - 1
                        parAudio_EpID.Value = tDBElement.ID
                        parAudio_StreamID.Value = i
                        parAudio_Language.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).Language
                        parAudio_LongLanguage.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).LongLanguage
                        parAudio_Codec.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).Codec
                        parAudio_Channel.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).Channels
                        parAudio_Bitrate.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Audio(i).Bitrate

                        SQLcommandTVAStreams.ExecuteNonQuery()
                    Next
                End Using

                'subtitles
                Using SQLcommandTVSubs As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommandTVSubs.CommandText = String.Concat("DELETE FROM TVSubs WHERE TVEpID = ", tDBElement.ID, ";")
                    SQLcommandTVSubs.ExecuteNonQuery()

                    SQLcommandTVSubs.CommandText = String.Concat("INSERT OR REPLACE INTO TVSubs (",
                       "TVEpID, StreamID, Subs_Language, Subs_LongLanguage, Subs_Type, Subs_Path, Subs_Forced",
                       ") VALUES (?,?,?,?,?,?,?);")
                    Dim parSubs_EpID As SQLiteParameter = SQLcommandTVSubs.Parameters.Add("parSubs_EpID", DbType.Int64, 0, "TVEpID")
                    Dim parSubs_StreamID As SQLiteParameter = SQLcommandTVSubs.Parameters.Add("parSubs_StreamID", DbType.Int64, 0, "StreamID")
                    Dim parSubs_Language As SQLiteParameter = SQLcommandTVSubs.Parameters.Add("parSubs_Language", DbType.String, 0, "Subs_Language")
                    Dim parSubs_LongLanguage As SQLiteParameter = SQLcommandTVSubs.Parameters.Add("parSubs_LongLanguage", DbType.String, 0, "Subs_LongLanguage")
                    Dim parSubs_Type As SQLiteParameter = SQLcommandTVSubs.Parameters.Add("parSubs_Type", DbType.String, 0, "Subs_Type")
                    Dim parSubs_Path As SQLiteParameter = SQLcommandTVSubs.Parameters.Add("parSubs_Path", DbType.String, 0, "Subs_Path")
                    Dim parSubs_Forced As SQLiteParameter = SQLcommandTVSubs.Parameters.Add("parSubs_Forced", DbType.Boolean, 0, "Subs_Forced")
                    Dim iID As Integer = 0
                    'embedded subtitles
                    For i As Integer = 0 To tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle.Count - 1
                        parSubs_EpID.Value = tDBElement.ID
                        parSubs_StreamID.Value = iID
                        parSubs_Language.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).Language
                        parSubs_LongLanguage.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).LongLanguage
                        parSubs_Type.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).SubsType
                        parSubs_Path.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).SubsPath
                        parSubs_Forced.Value = tDBElement.MainDetails.FileInfo.StreamDetails.Subtitle(i).SubsForced
                        SQLcommandTVSubs.ExecuteNonQuery()
                        iID += 1
                    Next
                    'external subtitles
                    If tDBElement.Subtitles IsNot Nothing Then
                        For i As Integer = 0 To tDBElement.Subtitles.Count - 1
                            parSubs_EpID.Value = tDBElement.ID
                            parSubs_StreamID.Value = iID
                            parSubs_Language.Value = tDBElement.Subtitles(i).Language
                            parSubs_LongLanguage.Value = tDBElement.Subtitles(i).LongLanguage
                            parSubs_Type.Value = tDBElement.Subtitles(i).SubsType
                            parSubs_Path.Value = tDBElement.Subtitles(i).SubsPath
                            parSubs_Forced.Value = tDBElement.Subtitles(i).SubsForced
                            SQLcommandTVSubs.ExecuteNonQuery()
                            iID += 1
                        Next
                    End If
                End Using

                If bDoSeasonCheck Then
                    Using SQLSeasonCheck As SQLiteCommand = _myvideosDBConn.CreateCommand()
                        SQLSeasonCheck.CommandText = String.Format("SELECT idSeason FROM seasons WHERE idShow = {0} AND Season = {1}", tDBElement.ShowID, tDBElement.MainDetails.Season)
                        Using SQLreader As SQLiteDataReader = SQLSeasonCheck.ExecuteReader()
                            If Not SQLreader.HasRows Then
                                Dim _season As New DBElement(Enums.ContentType.TVSeason) With {.ShowID = tDBElement.ShowID, .MainDetails = New MediaContainers.MainDetails With {.Season = tDBElement.MainDetails.Season}}
                                Save_TVSeason(_season, True, False, True)
                            End If
                        End Using
                    End Using
                End If
            End If
        End Using
        If Not bBatchMode Then SQLtransaction.Commit()

        If tDBElement.FileIDSpecified AndAlso bDoSync Then
            AddonsManager.Instance.RunGeneric(Enums.AddonEventType.Sync_TVEpisode, Nothing, Nothing, False, tDBElement)
        End If

        Return tDBElement
    End Function
    ''' <summary>
    ''' Stores information for a single season to the database
    ''' </summary>
    ''' <param name="tDBElement">Database.DBElement representing the season to be stored.</param>
    ''' <param name="bBatchMode"></param>
    ''' <remarks>Note that this stores the season information, not the individual episodes within that season</remarks>
    Public Function Save_TVSeason(ByRef tDBElement As DBElement, ByVal bBatchMode As Boolean, ByVal bToDisk As Boolean, ByVal bDoSync As Boolean) As DBElement
        If tDBElement.MainDetails Is Nothing Then Return tDBElement

        Dim doesExist As Boolean = False
        Dim ID As Long = -1

        Using SQLcommand_select_seasons As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand_select_seasons.CommandText = String.Format("SELECT idSeason FROM seasons WHERE idShow = {0} AND Season = {1}", tDBElement.ShowID, tDBElement.MainDetails.Season)
            Using SQLreader As SQLiteDataReader = SQLcommand_select_seasons.ExecuteReader()
                While SQLreader.Read
                    doesExist = True
                    ID = CInt(SQLreader("idSeason"))
                    Exit While
                End While
            End Using
        End Using

        Dim SQLtransaction As SQLiteTransaction = Nothing
        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()

        If Not doesExist Then
            Using SQLcommand_insert_seasons As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand_insert_seasons.CommandText = String.Concat("INSERT INTO seasons (",
                                                                      "idSeason, idShow, Season, SeasonText, Lock, Mark, New, TVDB, TMDB, Aired, Plot, DateModified",
                                                                      ") VALUES (NULL,?,?,?,?,?,?,?,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM seasons;")
                Dim par_seasons_idShow As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_idShow", DbType.Int64, 0, "idShow")
                Dim par_seasons_Season As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_Season", DbType.Int64, 0, "Season")
                Dim par_seasons_SeasonText As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_SeasonText", DbType.String, 0, "SeasonText")
                Dim par_seasons_Lock As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_Lock", DbType.Boolean, 0, "Lock")
                Dim par_seasons_Mark As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_Mark", DbType.Boolean, 0, "Mark")
                Dim par_seasons_New As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_New", DbType.Boolean, 0, "New")
                Dim par_seasons_TVDB As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_TVDB", DbType.Int32, 0, "TVDB")
                Dim par_seasons_TMDB As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_TMDB", DbType.Int32, 0, "TMDB")
                Dim par_seasons_Aired As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_Aired", DbType.String, 0, "Aired")
                Dim par_seasons_Plot As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_seasons_Plot", DbType.String, 0, "Plot")
                Dim par_seasons_DateModified As SQLiteParameter = SQLcommand_insert_seasons.Parameters.Add("par_season_DateModified", DbType.Int64, 0, "DateModified")
                par_seasons_idShow.Value = tDBElement.ShowID
                par_seasons_Season.Value = tDBElement.MainDetails.Season
                par_seasons_SeasonText.Value = If(tDBElement.MainDetails.TitleSpecified, tDBElement.MainDetails.Title, StringUtils.FormatSeasonText(tDBElement.MainDetails.Season))
                par_seasons_Lock.Value = tDBElement.IsLock
                par_seasons_Mark.Value = tDBElement.IsMark
                par_seasons_New.Value = True
                If tDBElement.MainDetails.TVDBSpecified Then 'need to be NOTHING instead of "0"
                    par_seasons_TVDB.Value = tDBElement.MainDetails.TVDB
                End If
                If tDBElement.MainDetails.TMDBSpecified Then 'need to be NOTHING instead of "0"
                    par_seasons_TMDB.Value = tDBElement.MainDetails.TMDB
                End If
                par_seasons_Aired.Value = tDBElement.MainDetails.Aired
                par_seasons_Plot.Value = tDBElement.MainDetails.Plot
                ID = CInt(SQLcommand_insert_seasons.ExecuteScalar())
            End Using
        Else
            Using SQLcommand_update_seasons As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLcommand_update_seasons.CommandText = String.Format("UPDATE seasons SET SeasonText=?, Lock=?, Mark=?, New=?, TVDB=?, TMDB=?, Aired=?, Plot=?, DateModified=? WHERE idSeason={0}", ID)
                Dim par_seasons_SeasonText As SQLiteParameter = SQLcommand_update_seasons.Parameters.Add("par_seasons_SeasonText", DbType.String, 0, "SeasonText")
                Dim par_seasons_Lock As SQLiteParameter = SQLcommand_update_seasons.Parameters.Add("par_seasons_Lock", DbType.Boolean, 0, "Lock")
                Dim par_seasons_Mark As SQLiteParameter = SQLcommand_update_seasons.Parameters.Add("par_seasons_Mark", DbType.Boolean, 0, "Mark")
                Dim par_seasons_New As SQLiteParameter = SQLcommand_update_seasons.Parameters.Add("par_seasons_New", DbType.Boolean, 0, "New")
                Dim par_seasons_TVDB As SQLiteParameter = SQLcommand_update_seasons.Parameters.Add("par_seasons_TVDB", DbType.Int32, 0, "TVDB")
                Dim par_seasons_TMDB As SQLiteParameter = SQLcommand_update_seasons.Parameters.Add("par_seasons_TMDB", DbType.Int32, 0, "TMDB")
                Dim par_seasons_Aired As SQLiteParameter = SQLcommand_update_seasons.Parameters.Add("par_seasons_Aired", DbType.String, 0, "Aired")
                Dim par_seasons_Plot As SQLiteParameter = SQLcommand_update_seasons.Parameters.Add("par_seasons_Plot", DbType.String, 0, "Plot")
                Dim par_seasons_DateModified As SQLiteParameter = SQLcommand_update_seasons.Parameters.Add("par_seasons_DateModified", DbType.Int64, 0, "DateModified")
                par_seasons_SeasonText.Value = If(tDBElement.MainDetails.TitleSpecified, tDBElement.MainDetails.Title, StringUtils.FormatSeasonText(tDBElement.MainDetails.Season))
                par_seasons_Lock.Value = tDBElement.IsLock
                par_seasons_Mark.Value = tDBElement.IsMark
                par_seasons_New.Value = False
                If tDBElement.MainDetails.TVDBSpecified Then 'need to be NOTHING instead of "0"
                    par_seasons_TVDB.Value = tDBElement.MainDetails.TVDB
                End If
                If tDBElement.MainDetails.TMDBSpecified Then 'need to be NOTHING instead of "0"
                    par_seasons_TMDB.Value = tDBElement.MainDetails.TMDB
                End If
                par_seasons_Aired.Value = tDBElement.MainDetails.Aired
                par_seasons_Plot.Value = tDBElement.MainDetails.Plot
                SQLcommand_update_seasons.ExecuteNonQuery()
            End Using
        End If

        tDBElement.ID = ID

        'Images
        If bToDisk Then tDBElement.ImagesContainer.SaveAllImages(tDBElement, False)

        Using SQLcommand_art As SQLiteCommand = _myvideosDBConn.CreateCommand()
            SQLcommand_art.CommandText = String.Concat("DELETE FROM art WHERE media_id = ", tDBElement.ID, " AND media_type = 'season';")
            SQLcommand_art.ExecuteNonQuery()
        End Using
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Banner.LocalFilePath) Then SetArtForItem(tDBElement.ID, "season", "banner", tDBElement.ImagesContainer.Banner.LocalFilePath)
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Fanart.LocalFilePath) Then SetArtForItem(tDBElement.ID, "season", "fanart", tDBElement.ImagesContainer.Fanart.LocalFilePath)
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Landscape.LocalFilePath) Then SetArtForItem(tDBElement.ID, "season", "landscape", tDBElement.ImagesContainer.Landscape.LocalFilePath)
        If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Poster.LocalFilePath) Then SetArtForItem(tDBElement.ID, "season", "poster", tDBElement.ImagesContainer.Poster.LocalFilePath)

        If Not bBatchMode Then SQLtransaction.Commit()

        If bDoSync Then
            AddonsManager.Instance.RunGeneric(Enums.AddonEventType.Sync_TVSeason, Nothing, Nothing, False, tDBElement)
        End If

        Return tDBElement
    End Function
    ''' <summary>
    ''' Saves all show information from a Database.DBElement object to the database
    ''' </summary>
    ''' <param name="tDBElement">Database.DBElement object to save to the database</param>
    ''' <param name="bBatchMode">Is the function already part of a transaction?</param>
    ''' <param name="bToDisk">Create NFO and Images</param>
    Public Function Save_TVShow(ByRef tDBElement As DBElement, ByVal bBatchMode As Boolean, ByVal bToNFO As Boolean, ByVal bToDisk As Boolean, ByVal bWithEpisodes As Boolean) As DBElement
        If tDBElement.MainDetails Is Nothing Then Return tDBElement

        Dim SQLtransaction As SQLiteTransaction = Nothing

        If Not bBatchMode Then SQLtransaction = _myvideosDBConn.BeginTransaction()
        Using SQLcommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
            If Not tDBElement.IDSpecified Then
                SQLcommand.CommandText = String.Concat("INSERT OR REPLACE INTO tvshow (",
                 "idSource, TVShowPath, New, Mark, TVDB, Lock, ListTitle, EpisodeGuide, ",
                 "Plot, Premiered, MPAA, Rating, NfoPath, Language, Ordering, ",
                 "Status, ThemePath, EFanartsPath, Runtime, Title, Votes, EpisodeSorting, SortTitle, ",
                 "IMDB, TMDB, OriginalTitle, UserRating, DateModified",
                 ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM tvshow;")
            Else
                SQLcommand.CommandText = String.Concat("INSERT OR REPLACE INTO tvshow (",
                 "idShow, idSource, TVShowPath, New, Mark, TVDB, Lock, ListTitle, EpisodeGuide, ",
                 "Plot, Premiered, MPAA, Rating, NfoPath, Language, Ordering, ",
                 "Status, ThemePath, EFanartsPath, Runtime, Title, Votes, EpisodeSorting, SortTitle, ",
                 "IMDB, TMDB, OriginalTitle, UserRating, DateModified",
                 ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?); SELECT LAST_INSERT_ROWID() FROM tvshow;")
                Dim par_lngTVShowID As SQLiteParameter = SQLcommand.Parameters.Add("parTVShowID", DbType.Int64, 0, "idShow")
                par_lngTVShowID.Value = tDBElement.ID
            End If

            Dim par_lngTVSourceID As SQLiteParameter = SQLcommand.Parameters.Add("parTVSourceID", DbType.Int64, 0, "idSource")
            Dim par_strTVShowPath As SQLiteParameter = SQLcommand.Parameters.Add("parTVShowPath", DbType.String, 0, "TVShowPath")
            Dim par_bNew As SQLiteParameter = SQLcommand.Parameters.Add("parNew", DbType.Boolean, 0, "new")
            Dim par_bMark As SQLiteParameter = SQLcommand.Parameters.Add("parMark", DbType.Boolean, 0, "mark")
            Dim par_TVDB As SQLiteParameter = SQLcommand.Parameters.Add("parTVDB", DbType.Int32, 0, "TVDB")
            Dim par_bLock As SQLiteParameter = SQLcommand.Parameters.Add("parLock", DbType.Boolean, 0, "lock")
            Dim par_strListTitle As SQLiteParameter = SQLcommand.Parameters.Add("parListTitle", DbType.String, 0, "ListTitle")
            Dim par_strEpisodeGuide As SQLiteParameter = SQLcommand.Parameters.Add("parEpisodeGuide", DbType.String, 0, "EpisodeGuide")
            Dim par_strPlot As SQLiteParameter = SQLcommand.Parameters.Add("parPlot", DbType.String, 0, "Plot")
            Dim par_strPremiered As SQLiteParameter = SQLcommand.Parameters.Add("parPremiered", DbType.String, 0, "Premiered")
            Dim par_strMPAA As SQLiteParameter = SQLcommand.Parameters.Add("parMPAA", DbType.String, 0, "MPAA")
            Dim par_strRating As SQLiteParameter = SQLcommand.Parameters.Add("parRating", DbType.String, 0, "Rating")
            Dim par_strNfoPath As SQLiteParameter = SQLcommand.Parameters.Add("parNfoPath", DbType.String, 0, "NfoPath")
            Dim par_strLanguage As SQLiteParameter = SQLcommand.Parameters.Add("parLanguage", DbType.String, 0, "Language")
            Dim par_iOrdering As SQLiteParameter = SQLcommand.Parameters.Add("parOrdering", DbType.Int16, 0, "Ordering")
            Dim par_strStatus As SQLiteParameter = SQLcommand.Parameters.Add("parStatus", DbType.String, 0, "Status")
            Dim par_strThemePath As SQLiteParameter = SQLcommand.Parameters.Add("parThemePath", DbType.String, 0, "ThemePath")
            Dim par_strExtrafanartsPath As SQLiteParameter = SQLcommand.Parameters.Add("parEFanartsPath", DbType.String, 0, "EFanartsPath")
            Dim par_strRuntime As SQLiteParameter = SQLcommand.Parameters.Add("parRuntime", DbType.String, 0, "Runtime")
            Dim par_strTitle As SQLiteParameter = SQLcommand.Parameters.Add("parTitle", DbType.String, 0, "Title")
            Dim par_strVotes As SQLiteParameter = SQLcommand.Parameters.Add("parVotes", DbType.String, 0, "Votes")
            Dim par_iEpisodeSorting As SQLiteParameter = SQLcommand.Parameters.Add("parEpisodeSorting", DbType.Int16, 0, "EpisodeSorting")
            Dim par_strSortTitle As SQLiteParameter = SQLcommand.Parameters.Add("parSortTitle", DbType.String, 0, "SortTitle")
            Dim par_IMDB As SQLiteParameter = SQLcommand.Parameters.Add("par_strIMDB", DbType.String, 0, "IMDB")
            Dim par_TMDB As SQLiteParameter = SQLcommand.Parameters.Add("par_strTMDB", DbType.Int32, 0, "TMDB")
            Dim par_OriginalTitle As SQLiteParameter = SQLcommand.Parameters.Add("par_OriginalTitle", DbType.String, 0, "OriginalTitle")
            Dim par_UserRating As SQLiteParameter = SQLcommand.Parameters.Add("par_UserRating", DbType.Int64, 0, "UserRating")
            Dim par_DateModified As SQLiteParameter = SQLcommand.Parameters.Add("par_DateModified", DbType.Int64, 0, "DateModified")

            'DateModified
            Try
                If Not tDBElement.IDSpecified AndAlso tDBElement.MainDetails.DateModifiedSpecified Then
                    Dim DateTimeDateModified As Date = Date.ParseExact(tDBElement.MainDetails.DateModified, "yyyy-MM-dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture)
                    par_DateModified.Value = Functions.ConvertToUnixTimestamp(DateTimeDateModified)
                ElseIf tDBElement.IDSpecified Then
                    par_DateModified.Value = Functions.ConvertToUnixTimestamp(Date.Now)
                End If
                If par_DateModified.Value IsNot Nothing Then
                    tDBElement.MainDetails.DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(par_DateModified.Value)).ToString("yyyy-MM-dd HH:mm:ss")
                Else
                    tDBElement.MainDetails.DateModified = String.Empty
                End If
            Catch
                par_DateModified.Value = If(Not tDBElement.IDSpecified, Functions.ConvertToUnixTimestamp(Date.Now), tDBElement.DateModified)
                tDBElement.MainDetails.DateModified = Functions.ConvertFromUnixTimestamp(Convert.ToInt64(par_DateModified.Value)).ToString("yyyy-MM-dd HH:mm:ss")
            End Try

            With tDBElement.MainDetails
                par_UserRating.Value = .UserRating
                par_strEpisodeGuide.Value = .EpisodeGuide.URL
                par_IMDB.Value = .IMDB
                par_strMPAA.Value = .MPAA
                par_OriginalTitle.Value = .OriginalTitle
                par_strPlot.Value = .Plot
                par_strPremiered.Value = NumUtils.DateToISO8601Date(.Premiered)
                par_strRating.Value = .Rating
                par_strRuntime.Value = .Runtime
                par_strSortTitle.Value = .SortTitle
                par_strStatus.Value = .Status
                If .TMDBSpecified Then 'need to be NOTHING instead of "0"
                    par_TMDB.Value = .TMDB
                End If
                If .TVDBSpecified Then 'need to be NOTHING instead of "0"
                    par_TVDB.Value = .TVDB
                End If
                par_strTitle.Value = .Title
                par_strVotes.Value = .Votes
            End With

            'First let's save it to NFO, even because we will need the NFO path
            'Also Save Images to get ExtrafanartsPath
            'art Table be be linked later
            If bToNFO Then NFO.SaveToNFO_TVShow(tDBElement)
            If bToDisk Then
                tDBElement.ImagesContainer.SaveAllImages(tDBElement, False)
                tDBElement.Theme.SaveAllThemes(tDBElement, False)
                tDBElement.MainDetails.SaveAllActorThumbs(tDBElement)
            End If

            par_strExtrafanartsPath.Value = tDBElement.ExtrafanartsPath
            par_strNfoPath.Value = tDBElement.NfoPath
            par_strThemePath.Value = If(Not String.IsNullOrEmpty(tDBElement.Theme.LocalFilePath), tDBElement.Theme.LocalFilePath, String.Empty)
            par_strTVShowPath.Value = tDBElement.ShowPath

            par_bNew.Value = Not tDBElement.IDSpecified
            par_strListTitle.Value = tDBElement.ListTitle
            par_bMark.Value = tDBElement.IsMark
            par_bLock.Value = tDBElement.IsLock
            par_lngTVSourceID.Value = tDBElement.Source.ID
            par_strLanguage.Value = tDBElement.Language
            par_iOrdering.Value = tDBElement.Ordering
            par_iEpisodeSorting.Value = tDBElement.EpisodeSorting

            If Not tDBElement.IDSpecified Then
                If Master.eSettings.TVGeneralMarkNewShows Then
                    par_bMark.Value = True
                    tDBElement.IsMark = True
                End If
                Using rdrTVShow As SQLiteDataReader = SQLcommand.ExecuteReader()
                    If rdrTVShow.Read Then
                        tDBElement.ID = Convert.ToInt64(rdrTVShow(0))
                        tDBElement.ShowID = tDBElement.ID
                    Else
                        logger.Error("Something very wrong here: SaveTVShowToDB", tDBElement.ToString, "Error")
                        tDBElement.ID = -1
                        tDBElement.ShowID = tDBElement.ID
                        Return tDBElement
                        Exit Function
                    End If
                End Using
            Else
                SQLcommand.ExecuteNonQuery()
            End If

            If Not tDBElement.ID = -1 Then

                'Actors
                Using SQLcommand_actorlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_actorlink.CommandText = String.Format("DELETE FROM actorlinktvshow WHERE idShow = {0};", tDBElement.ID)
                    SQLcommand_actorlink.ExecuteNonQuery()
                End Using
                AddCast(tDBElement.ID, "tvshow", "show", tDBElement.MainDetails.Actors)

                'Creators
                Using SQLcommand_creatorlink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_creatorlink.CommandText = String.Format("DELETE FROM creatorlinktvshow WHERE idShow = {0};", tDBElement.ID)
                    SQLcommand_creatorlink.ExecuteNonQuery()
                End Using
                For Each creator As String In tDBElement.MainDetails.Creators
                    AddCreatorToTvShow(tDBElement.ID, AddActor(creator, "", "", "", -1, False))
                Next

                'Countries
                Using SQLcommand_countrylink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_countrylink.CommandText = String.Format("DELETE FROM countrylinktvshow WHERE idShow = {0};", tDBElement.ID)
                    SQLcommand_countrylink.ExecuteNonQuery()
                End Using
                For Each country As String In tDBElement.MainDetails.Countries
                    AddCountryToTVShow(tDBElement.ID, AddCountry(country))
                Next

                'Genres
                Using SQLcommand_genrelink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_genrelink.CommandText = String.Format("DELETE FROM genrelinktvshow WHERE idShow = {0};", tDBElement.ID)
                    SQLcommand_genrelink.ExecuteNonQuery()
                End Using
                For Each genre As String In tDBElement.MainDetails.Genres
                    AddGenreToTvShow(tDBElement.ID, AddGenre(genre))
                Next

                'Images
                Using SQLcommand_art As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_art.CommandText = String.Format("DELETE FROM art WHERE media_id = {0} AND media_type = 'tvshow';", tDBElement.ID)
                    SQLcommand_art.ExecuteNonQuery()
                End Using
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Banner.LocalFilePath) Then SetArtForItem(tDBElement.ID, "tvshow", "banner", tDBElement.ImagesContainer.Banner.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.CharacterArt.LocalFilePath) Then SetArtForItem(tDBElement.ID, "tvshow", "characterart", tDBElement.ImagesContainer.CharacterArt.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.ClearArt.LocalFilePath) Then SetArtForItem(tDBElement.ID, "tvshow", "clearart", tDBElement.ImagesContainer.ClearArt.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.ClearLogo.LocalFilePath) Then SetArtForItem(tDBElement.ID, "tvshow", "clearlogo", tDBElement.ImagesContainer.ClearLogo.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Fanart.LocalFilePath) Then SetArtForItem(tDBElement.ID, "tvshow", "fanart", tDBElement.ImagesContainer.Fanart.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Landscape.LocalFilePath) Then SetArtForItem(tDBElement.ID, "tvshow", "landscape", tDBElement.ImagesContainer.Landscape.LocalFilePath)
                If Not String.IsNullOrEmpty(tDBElement.ImagesContainer.Poster.LocalFilePath) Then SetArtForItem(tDBElement.ID, "tvshow", "poster", tDBElement.ImagesContainer.Poster.LocalFilePath)

                'Studios
                Using SQLcommand_studiolink As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_studiolink.CommandText = String.Format("DELETE FROM studiolinktvshow WHERE idShow = {0};", tDBElement.ID)
                    SQLcommand_studiolink.ExecuteNonQuery()
                End Using
                For Each studio As String In tDBElement.MainDetails.Studios
                    AddStudioToTvShow(tDBElement.ID, AddStudio(studio))
                Next

                'Tags
                Using SQLcommand_taglinks As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLcommand_taglinks.CommandText = String.Format("DELETE FROM taglinks WHERE idMedia = {0} AND media_type = 'tvshow';", tDBElement.ID)
                    SQLcommand_taglinks.ExecuteNonQuery()
                End Using
                For Each tag As String In tDBElement.MainDetails.Tags
                    AddTagToItem(tDBElement.ID, AddTag(tag), "tvshow")
                Next

            End If
        End Using

        'save season informations
        If tDBElement.SeasonsSpecified Then
            For Each nSeason As DBElement In tDBElement.Seasons
                Save_TVSeason(nSeason, True, True, True)
            Next
            Delete_Invalid_TVSeasons(tDBElement.Seasons, tDBElement.ID, True)
        End If

        'save episode informations
        If bWithEpisodes AndAlso tDBElement.EpisodesSpecified Then
            For Each nEpisode As DBElement In tDBElement.Episodes
                Save_TVEpisode(nEpisode, True, True, True, False, True)
            Next
            Delete_Invalid_TVEpisodes(tDBElement.Episodes, tDBElement.ID, True)
        End If

        'delete empty seasons after saving all known episodes
        Delete_Empty_TVSeasons(tDBElement.ID, True)

        If Not bBatchMode Then SQLtransaction.Commit()

        AddonsManager.Instance.RunGeneric(Enums.AddonEventType.Sync_TVShow, Nothing, Nothing, False, tDBElement)

        Return tDBElement
    End Function


    '''''''''''''''''''''''''''''''''''''''''''
    'Protected Sub ConnectJobsDB()
    '    If _myvideosDBConn IsNot Nothing Then
    '        Return
    '        'Throw New InvalidOperationException("A database connection is already open, can't open another.")
    '    End If

    '    Dim jobsDBFile As String = Path.Combine(Functions.AppPath, "JobLogs.emm")
    '    Dim isNew As Boolean = (Not File.Exists(jobsDBFile))

    '    Try
    '        _jobsDBConn = New SQLiteConnection(String.Format(_connStringTemplate, jobsDBFile))
    '        _jobsDBConn.Open()
    '    Catch ex As Exception
    '        logger.Error(GetType(Database),ex.ToString, _
    '                                    ex.StackTrace, _
    '                                    "Unable to open media database connection.")
    '    End Try

    '    If isNew Then
    '        Dim sqlCommand As String = My.Resources.JobsDatabaseSQL_v1
    '        Using transaction As SQLite.SQLiteTransaction = _jobsDBConn.BeginTransaction()
    '            Using command As SQLite.SQLiteCommand = _jobsDBConn.CreateCommand()
    '                command.CommandText = sqlCommand
    '                command.ExecuteNonQuery()
    '            End Using
    '            transaction.Commit()
    '        End Using
    '    End If
    'End Sub

    ''' <summary>
    ''' Verify whether the given Addon is installed
    ''' </summary>
    ''' <param name="intAddonID">The AddonID to be verified.</param>
    ''' <returns>Version of the addon, if it is installed, or zero (0) otherwise</returns>
    ''' <remarks></remarks>
    Public Function IsAddonInstalled(ByVal intAddonID As Integer) As Single
        If intAddonID < 0 Then
            logger.Error(New StackFrame().GetMethod().Name, Environment.StackTrace, "Invalid AddonID: {0}" & intAddonID)
            'Throw New ArgumentOutOfRangeException("AddonID", "Must be a positive integer")
        End If

        Try
            Using SQLCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                SQLCommand.CommandText = String.Concat("SELECT Version FROM Addons WHERE AddonID = ", intAddonID, ";")
                Dim tES As Object = SQLCommand.ExecuteScalar
                If tES IsNot Nothing Then
                    Dim tSing As Single = 0
                    If Single.TryParse(tES.ToString, tSing) Then
                        Return tSing
                    End If
                End If
            End Using
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
        End Try
        Return 0
    End Function
    ''' <summary>
    ''' Removes the referenced Addon from the list of installed Addons
    ''' </summary>
    ''' <param name="intAddonID"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function UninstallAddon(ByVal intAddonID As Integer) As Boolean
        If intAddonID < 0 Then
            logger.Error(New StackFrame().GetMethod().Name, Environment.StackTrace, "Invalid AddonID: {0}" & intAddonID)
            'Throw New ArgumentOutOfRangeException("AddonID", "Must be a positive integer")
        End If

        Dim needRestart As Boolean = False
        Try
            Dim _cmds As Containers.InstallCommands = Containers.InstallCommands.Load(Path.Combine(Functions.AppPath, "InstallTasks.xml"))
            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Using SQLCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLCommand.CommandText = String.Concat("SELECT FilePath FROM AddonFiles WHERE AddonID = ", intAddonID, ";")
                    Using SQLReader As SQLiteDataReader = SQLCommand.ExecuteReader
                        While SQLReader.Read
                            Try
                                File.Delete(SQLReader("FilePath").ToString)
                            Catch
                                _cmds.noTransaction.Add(New Containers.CommandsNoTransactionCommand With {.type = "FILE.Delete", .execute = SQLReader("FilePath").ToString})
                                needRestart = True
                            End Try
                        End While
                        If needRestart Then _cmds.Save(Path.Combine(Functions.AppPath, "InstallTasks.xml"))
                    End Using
                    SQLCommand.CommandText = String.Concat("DELETE FROM Addons WHERE AddonID = ", intAddonID, ";")
                    SQLCommand.ExecuteNonQuery()
                    SQLCommand.CommandText = String.Concat("DELETE FROM AddonFiles WHERE AddonID = ", intAddonID, ";")
                    SQLCommand.ExecuteNonQuery()
                End Using
                SQLtransaction.Commit()
            End Using
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
        End Try
        Return Not needRestart
    End Function

    ''' <summary>
    ''' Saves/installs the supplied Addon to the database
    ''' </summary>
    ''' <param name="intAddon">Addon to be saved</param>
    ''' <remarks></remarks>
    Public Sub SaveAddonToDB(ByVal intAddon As Containers.Addon)
        'TODO Need to add validation on Addon.ID, especially if it is passed in the parameter
        If intAddon Is Nothing Then
            logger.Error(New StackFrame().GetMethod().Name, Environment.StackTrace, "Invalid AddonID: empty")
        End If
        Try
            Using SQLtransaction As SQLiteTransaction = _myvideosDBConn.BeginTransaction()
                Using SQLCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                    SQLCommand.CommandText = String.Concat("INSERT OR REPLACE INTO Addons (",
                      "AddonID, Version) VALUES (?,?);")
                    Dim parAddonID As SQLiteParameter = SQLCommand.Parameters.Add("parAddonID", DbType.Int64, 0, "AddonID")
                    Dim parVersion As SQLiteParameter = SQLCommand.Parameters.Add("parVersion", DbType.String, 0, "Version")

                    parAddonID.Value = intAddon.ID
                    parVersion.Value = intAddon.Version.ToString

                    SQLCommand.ExecuteNonQuery()

                    SQLCommand.CommandText = String.Concat("DELETE FROM AddonFiles WHERE AddonID = ", intAddon.ID, ";")
                    SQLCommand.ExecuteNonQuery()

                    Using SQLFileCommand As SQLiteCommand = _myvideosDBConn.CreateCommand()
                        SQLFileCommand.CommandText = String.Concat("INSERT INTO AddonFiles (AddonID, FilePath) VALUES (?,?);")
                        Dim parFileAddonID As SQLiteParameter = SQLFileCommand.Parameters.Add("parFileAddonID", DbType.Int64, 0, "AddonID")
                        Dim parFilePath As SQLiteParameter = SQLFileCommand.Parameters.Add("parFilePath", DbType.String, 0, "FilePath")
                        parFileAddonID.Value = intAddon.ID
                        For Each fFile As KeyValuePair(Of String, String) In intAddon.Files
                            parFilePath.Value = Path.Combine(Functions.AppPath, fFile.Key.Replace("/", Path.DirectorySeparatorChar))
                            SQLFileCommand.ExecuteNonQuery()
                        Next
                    End Using
                End Using
                SQLtransaction.Commit()
            End Using
        Catch ex As Exception
            logger.Error(ex, New StackFrame().GetMethod().Name)
        End Try
    End Sub
    ''' <summary>
    ''' Check if provided querystring is valid SQL
    ''' </summary>
    ''' <param name="strQuery">The SQL query to check</param>
    ''' <returns>true: valid query, false: invalid sql (check log!)</returns>
    ''' <remarks>    
    ''' cocotus 2015/03/07 Check for valid sql syntax, used for custom filter module
    ''' </remarks>
    Public Function IsValid_SQL(ByVal strQuery As String) As Boolean
        Try
            Using SQLcommand As SQLiteCommand = Master.DB.MyVideosDBConn.CreateCommand()
                SQLcommand.CommandText = strQuery
                Using SQLreader As SQLiteDataReader = SQLcommand.ExecuteReader()
                End Using
            End Using
            Return True
        Catch ex As Exception
            Dim response As String = String.Empty
            response = Master.eLang.GetString(1386, "Invalid SQL!") & Environment.NewLine & ex.Message
            MessageBox.Show(ex.Message, Master.eLang.GetString(356, "Warning"), MessageBoxButtons.OK)
            Return False
        End Try
    End Function

#End Region 'Methods

#Region "Nested Types"

    Private Structure Arguments

#Region "Fields"

        Dim currDBPath As String
        Dim currVersion As Integer
        Dim newDBPath As String
        Dim newVersion As Integer

#End Region 'Fields

    End Structure

    Public Class SQLViewProperty

#Region "Fields"

        Private _name As String
        Private _statement As String

#End Region 'Fields

#Region "Constructors"

        Public Sub New()
            Clear()
        End Sub

#End Region 'Constructors

#Region "Properties"

        Public Property Name() As String
            Get
                Return _name
            End Get
            Set(ByVal value As String)
                _name = value
            End Set
        End Property
        Public Property Statement() As String
            Get
                Return _statement
            End Get
            Set(ByVal value As String)
                _statement = value
            End Set
        End Property

#End Region 'Properties

#Region "Methods"

        Public Sub Clear()
            _name = String.Empty
            _statement = String.Empty
        End Sub

#End Region 'Methods

    End Class

    <Serializable()>
    Public Class DBElement
        Implements ICloneable

#Region "Fields"

        Private _actorthumbs As New List(Of String)
        Private _contenttype As Enums.ContentType
        Private _dateadded As Long
        Private _datemodified As Long
        Private _episodes As New List(Of DBElement)
        Private _episodesorting As Enums.EpisodeSorting
        Private _extrafanartspath As String
        Private _extrathumbspath As String
        Private _fileid As Long
        Private _fileitem As FileItem
        Private _id As Long
        Private _imagescontainer As New MediaContainers.ImagesContainer
        Private _islock As Boolean
        Private _ismark As Boolean
        Private _ismarkcustom1 As Boolean
        Private _ismarkcustom2 As Boolean
        Private _ismarkcustom3 As Boolean
        Private _ismarkcustom4 As Boolean
        Private _isonline As Boolean
        Private _issingle As Boolean
        Private _language As String
        Private _listtitle As String
        Private _maindetails As MediaContainers.MainDetails
        Private _moviesinset As List(Of MediaContainers.MovieInSet)
        Private _nfopath As String
        Private _ordering As Enums.EpisodeOrdering
        Private _outoftolerance As Boolean
        Private _scrapemodifiers As Structures.ScrapeModifiers
        Private _scrapeoptions As Structures.ScrapeOptions
        Private _scrapetype As Enums.ScrapeType
        Private _seasons As New List(Of DBElement)
        Private _showid As Long
        Private _showpath As String
        Private _sortmethod As Enums.SortMethod_MovieSet
        Private _source As New DBSource
        Private _subtitles As New List(Of MediaContainers.Subtitle)
        Private _theme As New MediaContainers.Theme
        Private _trailer As New MediaContainers.Trailer
        Private _tvshowdetails As MediaContainers.MainDetails
        Private _videosource As String

#End Region 'Fields

#Region "Constructors"

        Public Sub New(ByVal ContentType As Enums.ContentType)
            Clear()
            _contenttype = ContentType
        End Sub

#End Region 'Constructors

#Region "Properties"

        Public Property ActorThumbs() As List(Of String)
            Get
                Return _actorthumbs
            End Get
            Set(ByVal value As List(Of String))
                _actorthumbs = value
            End Set
        End Property

        Public ReadOnly Property ActorThumbsSpecified() As Boolean
            Get
                Return _actorthumbs.Count > 0
            End Get
        End Property

        Public ReadOnly Property ContentType() As Enums.ContentType
            Get
                Return _contenttype
            End Get
        End Property

        Public Property DateAdded() As Long
            Get
                Return _dateadded
            End Get
            Set(ByVal value As Long)
                _dateadded = value
            End Set
        End Property

        Public Property DateModified() As Long
            Get
                Return _datemodified
            End Get
            Set(ByVal value As Long)
                _datemodified = value
            End Set
        End Property

        Public Property Episodes() As List(Of DBElement)
            Get
                Return _episodes
            End Get
            Set(ByVal value As List(Of DBElement))
                _episodes = value
            End Set
        End Property

        Public ReadOnly Property EpisodesSpecified() As Boolean
            Get
                Return _episodes.Count > 0
            End Get
        End Property

        Public Property EpisodeSorting() As Enums.EpisodeSorting
            Get
                Return _episodesorting
            End Get
            Set(ByVal value As Enums.EpisodeSorting)
                _episodesorting = value
            End Set
        End Property

        Public Property ExtrafanartsPath() As String
            Get
                Return _extrafanartspath
            End Get
            Set(ByVal value As String)
                _extrafanartspath = value
            End Set
        End Property

        Public ReadOnly Property ExtrafanartsPathSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_extrafanartspath)
            End Get
        End Property

        Public Property ExtrathumbsPath() As String
            Get
                Return _extrathumbspath
            End Get
            Set(ByVal value As String)
                _extrathumbspath = value
            End Set
        End Property

        Public ReadOnly Property ExtrathumbsPathSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_extrathumbspath)
            End Get
        End Property

        Public Property FileID() As Long
            Get
                Return _fileid
            End Get
            Set(ByVal value As Long)
                _fileid = value
            End Set
        End Property

        Public ReadOnly Property FileIDSpecified() As Boolean
            Get
                Return Not _fileid = -1
            End Get
        End Property

        Public Property FileItem() As FileItem
            Get
                Return _fileitem
            End Get
            Set(ByVal value As FileItem)
                _fileitem = value
            End Set
        End Property

        Public ReadOnly Property FileItemSpecified() As Boolean
            Get
                Return _fileitem IsNot Nothing
            End Get
        End Property

        Public ReadOnly Property FilenameSpecified() As Boolean
            Get
                Return _fileitem IsNot Nothing AndAlso Not String.IsNullOrEmpty(_fileitem.FullPath)
            End Get
        End Property

        Public Property ID() As Long
            Get
                Return _id
            End Get
            Set(ByVal value As Long)
                _id = value
            End Set
        End Property

        Public ReadOnly Property IDSpecified() As Boolean
            Get
                Return Not _id = -1
            End Get
        End Property

        Public Property ImagesContainer() As MediaContainers.ImagesContainer
            Get
                Return _imagescontainer
            End Get
            Set(ByVal value As MediaContainers.ImagesContainer)
                _imagescontainer = value
            End Set
        End Property

        Public Property IsLock() As Boolean
            Get
                Return _islock
            End Get
            Set(ByVal value As Boolean)
                _islock = value
                If _maindetails IsNot Nothing Then _maindetails.Locked = value
            End Set
        End Property

        Public Property IsMark() As Boolean
            Get
                Return _ismark
            End Get
            Set(ByVal value As Boolean)
                _ismark = value
            End Set
        End Property

        Public Property IsMarkCustom1() As Boolean
            Get
                Return _ismarkcustom1
            End Get
            Set(ByVal value As Boolean)
                _ismarkcustom1 = value
            End Set
        End Property

        Public Property IsMarkCustom2() As Boolean
            Get
                Return _ismarkcustom2
            End Get
            Set(ByVal value As Boolean)
                _ismarkcustom2 = value
            End Set
        End Property

        Public Property IsMarkCustom3() As Boolean
            Get
                Return _ismarkcustom3
            End Get
            Set(ByVal value As Boolean)
                _ismarkcustom3 = value
            End Set
        End Property

        Public Property IsMarkCustom4() As Boolean
            Get
                Return _ismarkcustom4
            End Get
            Set(ByVal value As Boolean)
                _ismarkcustom4 = value
            End Set
        End Property

        Public Property IsOnline() As Boolean
            Get
                Return _isonline
            End Get
            Set(ByVal value As Boolean)
                _isonline = value
            End Set
        End Property

        Public Property IsSingle() As Boolean
            Get
                Return _issingle
            End Get
            Set(ByVal value As Boolean)
                _issingle = value
            End Set
        End Property

        Public Property Language() As String
            Get
                Return _language
            End Get
            Set(ByVal value As String)
                _language = value
            End Set
        End Property

        Public ReadOnly Property LanguageSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_language)
            End Get
        End Property

        Public ReadOnly Property Language_Main() As String
            Get
                Return Regex.Replace(_language, "-.*", String.Empty).Trim
            End Get
        End Property

        Public Property ListTitle() As String
            Get
                Return _listtitle
            End Get
            Set(ByVal value As String)
                _listtitle = value
            End Set
        End Property

        Public ReadOnly Property ListTitleSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_listtitle)
            End Get
        End Property

        Public Property MainDetails() As MediaContainers.MainDetails
            Get
                Return _maindetails
            End Get
            Set(ByVal value As MediaContainers.MainDetails)
                _maindetails = value
            End Set
        End Property

        Public ReadOnly Property MainDetailsSpecified() As Boolean
            Get
                Return _maindetails IsNot Nothing
            End Get
        End Property

        Public Property MoviesInSet() As List(Of MediaContainers.MovieInSet)
            Get
                Return _moviesinset
            End Get
            Set(ByVal value As List(Of MediaContainers.MovieInSet))
                _moviesinset = value
            End Set
        End Property

        Public ReadOnly Property MoviesInSetSpecified() As Boolean
            Get
                Return _moviesinset.Count > 0
            End Get
        End Property

        Public Property NfoPath() As String
            Get
                Return _nfopath
            End Get
            Set(ByVal value As String)
                _nfopath = value
            End Set
        End Property

        Public ReadOnly Property NfoPathSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_nfopath)
            End Get
        End Property

        Public Property Ordering() As Enums.EpisodeOrdering
            Get
                Return _ordering
            End Get
            Set(ByVal value As Enums.EpisodeOrdering)
                _ordering = value
            End Set
        End Property

        Public Property OutOfTolerance() As Boolean
            Get
                Return _outoftolerance
            End Get
            Set(ByVal value As Boolean)
                _outoftolerance = value
            End Set
        End Property

        Public Property ScrapeModifiers() As Structures.ScrapeModifiers
            Get
                Return _scrapemodifiers
            End Get
            Set(ByVal value As Structures.ScrapeModifiers)
                _scrapemodifiers = value
            End Set
        End Property

        Public Property ScrapeOptions() As Structures.ScrapeOptions
            Get
                Return _scrapeoptions
            End Get
            Set(ByVal value As Structures.ScrapeOptions)
                _scrapeoptions = value
            End Set
        End Property

        Public Property ScrapeType() As Enums.ScrapeType
            Get
                Return _scrapetype
            End Get
            Set(ByVal value As Enums.ScrapeType)
                _scrapetype = value
            End Set
        End Property

        Public Property Seasons() As List(Of DBElement)
            Get
                Return _seasons
            End Get
            Set(ByVal value As List(Of DBElement))
                _seasons = value
            End Set
        End Property

        Public ReadOnly Property SeasonsSpecified() As Boolean
            Get
                Return _seasons.Count > 0
            End Get
        End Property

        Public Property ShowID() As Long
            Get
                Return _showid
            End Get
            Set(ByVal value As Long)
                _showid = value
            End Set
        End Property

        Public ReadOnly Property ShowIDSpecified() As Boolean
            Get
                Return Not _showid = -1
            End Get
        End Property

        Public Property ShowPath() As String
            Get
                Return _showpath
            End Get
            Set(ByVal value As String)
                _showpath = value
            End Set
        End Property

        Public ReadOnly Property ShowPathSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_showpath)
            End Get
        End Property

        Public Property SortMethod() As Enums.SortMethod_MovieSet
            Get
                Return _sortmethod
            End Get
            Set(ByVal value As Enums.SortMethod_MovieSet)
                _sortmethod = value
            End Set
        End Property

        Public Property Source() As DBSource
            Get
                Return _source
            End Get
            Set(ByVal value As DBSource)
                _source = value
            End Set
        End Property

        Public ReadOnly Property SourceSpecified() As Boolean
            Get
                Return Not _source.ID = -1
            End Get
        End Property

        Public Property Subtitles() As List(Of MediaContainers.Subtitle)
            Get
                Return _subtitles
            End Get
            Set(ByVal value As List(Of MediaContainers.Subtitle))
                _subtitles = value
            End Set
        End Property

        Public ReadOnly Property SubtitlesSpecified() As Boolean
            Get
                Return _subtitles.Count > 0
            End Get
        End Property

        Public Property Theme() As MediaContainers.Theme
            Get
                Return _theme
            End Get
            Set(ByVal value As MediaContainers.Theme)
                _theme = value
            End Set
        End Property

        Public ReadOnly Property ThemeSpecified() As Boolean
            Get
                Return _theme.ThemeOriginal IsNot Nothing AndAlso _theme.ThemeOriginal.hasMemoryStream
            End Get
        End Property

        Public Property Trailer() As MediaContainers.Trailer
            Get
                Return _trailer
            End Get
            Set(ByVal value As MediaContainers.Trailer)
                _trailer = value
            End Set
        End Property

        Public ReadOnly Property TrailerSpecified() As Boolean
            Get
                Return _trailer.TrailerOriginal IsNot Nothing AndAlso _trailer.TrailerOriginal.hasMemoryStream
            End Get
        End Property
        ''' <summary>
        ''' Only to set the TVShow informations for TVEpisodes and TVSeasons elements
        ''' </summary>
        ''' <returns></returns>
        Public Property TVShowDetails() As MediaContainers.MainDetails
            Get
                Return _tvshowdetails
            End Get
            Set(ByVal value As MediaContainers.MainDetails)
                _tvshowdetails = value
            End Set
        End Property

        Public ReadOnly Property TVShowSpecified() As Boolean
            Get
                Return _tvshowdetails IsNot Nothing
            End Get
        End Property

        Public Property VideoSource() As String
            Get
                Return _videosource
            End Get
            Set(ByVal value As String)
                _videosource = value
            End Set
        End Property

        Public ReadOnly Property VideoSourceSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_videosource)
            End Get
        End Property

#End Region 'Properties

#Region "Methods"

        Public Sub Clear()
            _actorthumbs = New List(Of String)
            _dateadded = -1
            _datemodified = -1
            _episodes = New List(Of DBElement)
            _episodesorting = Enums.EpisodeSorting.Episode
            _extrafanartspath = String.Empty
            _extrathumbspath = String.Empty
            _fileid = -1
            _id = -1
            _imagescontainer = New MediaContainers.ImagesContainer
            _islock = False
            _ismark = False
            _isonline = False
            _issingle = False
            _language = String.Empty
            _listtitle = String.Empty
            _maindetails = New MediaContainers.MainDetails 'Nothing
            _moviesinset = New List(Of MediaContainers.MovieInSet)
            _nfopath = String.Empty
            _ordering = Enums.EpisodeOrdering.Standard
            _outoftolerance = False
            _seasons = New List(Of DBElement)
            _showid = -1
            _showpath = String.Empty
            _sortmethod = Enums.SortMethod_MovieSet.Year
            _source = New DBSource
            _subtitles = New List(Of MediaContainers.Subtitle)
            _theme = New MediaContainers.Theme
            _trailer = New MediaContainers.Trailer
            _tvshowdetails = Nothing
            _videosource = String.Empty
        End Sub

        Public Function CloneDeep() As Object Implements ICloneable.Clone
            Dim Stream As New MemoryStream(50000)
            Dim Formatter As New Runtime.Serialization.Formatters.Binary.BinaryFormatter()
            ' Serialisierung über alle Objekte hinweg in einen Stream 
            Formatter.Serialize(Stream, Me)
            ' Zurück zum Anfang des Streams und... 
            Stream.Seek(0, SeekOrigin.Begin)
            ' ...aus dem Stream in ein Objekt deserialisieren 
            CloneDeep = Formatter.Deserialize(Stream)
            Stream.Close()
        End Function

        Public Sub LoadAllImages(ByVal LoadBitmap As Boolean, ByVal withExtraImages As Boolean)
            ImagesContainer.LoadAllImages(ContentType, LoadBitmap, withExtraImages)
        End Sub

#End Region 'Methods

    End Class

    <Serializable()>
    Public Class DBSource

#Region "Fields"

        Private _episodesorting As Enums.EpisodeSorting
        Private _exclude As Boolean
        Private _getyear As Boolean
        Private _id As Long
        Private _issingle As Boolean
        Private _language As String
        Private _lastscan As String
        Private _name As String
        Private _ordering As Enums.EpisodeOrdering
        Private _path As String
        Private _recursive As Boolean
        Private _usefoldername As Boolean

#End Region 'Fields

#Region "Constructors"

        Public Sub New()
            Clear()
        End Sub

#End Region 'Constructors

#Region "Properties"

        Public Property EpisodeSorting() As Enums.EpisodeSorting
            Get
                Return _episodesorting
            End Get
            Set(ByVal value As Enums.EpisodeSorting)
                _episodesorting = value
            End Set
        End Property

        Public Property Exclude() As Boolean
            Get
                Return _exclude
            End Get
            Set(ByVal value As Boolean)
                _exclude = value
            End Set
        End Property

        Public Property GetYear() As Boolean
            Get
                Return _getyear
            End Get
            Set(ByVal value As Boolean)
                _getyear = value
            End Set
        End Property

        Public Property ID() As Long
            Get
                Return _id
            End Get
            Set(ByVal value As Long)
                _id = value
            End Set
        End Property

        Public ReadOnly Property IDSpecified() As Boolean
            Get
                Return Not _id = -1
            End Get
        End Property

        Public Property IsSingle() As Boolean
            Get
                Return _issingle
            End Get
            Set(ByVal value As Boolean)
                _issingle = value
            End Set
        End Property

        Public Property Language() As String
            Get
                Return _language
            End Get
            Set(ByVal value As String)
                _language = value
            End Set
        End Property

        Public ReadOnly Property LanguageSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_language)
            End Get
        End Property

        Public Property LastScan() As String
            Get
                Return _lastscan
            End Get
            Set(ByVal value As String)
                _lastscan = value
            End Set
        End Property

        Public ReadOnly Property LastScanSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_lastscan)
            End Get
        End Property

        Public Property Name() As String
            Get
                Return _name
            End Get
            Set(ByVal value As String)
                _name = value
            End Set
        End Property

        Public ReadOnly Property NameSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_name)
            End Get
        End Property

        Public Property Ordering() As Enums.EpisodeOrdering
            Get
                Return _ordering
            End Get
            Set(ByVal value As Enums.EpisodeOrdering)
                _ordering = value
            End Set
        End Property

        Public Property Path() As String
            Get
                Return _path
            End Get
            Set(ByVal value As String)
                _path = value
            End Set
        End Property

        Public ReadOnly Property PathSpecified() As Boolean
            Get
                Return Not String.IsNullOrEmpty(_path)
            End Get
        End Property

        Public Property Recursive() As Boolean
            Get
                Return _recursive
            End Get
            Set(ByVal value As Boolean)
                _recursive = value
            End Set
        End Property

        Public Property UseFolderName() As Boolean
            Get
                Return _usefoldername
            End Get
            Set(ByVal value As Boolean)
                _usefoldername = value
            End Set
        End Property

#End Region 'Properties

#Region "Methods"

        Public Sub Clear()
            _episodesorting = Enums.EpisodeSorting.Episode
            _exclude = False
            _getyear = False
            _id = -1
            _issingle = False
            _language = String.Empty
            _lastscan = String.Empty
            _name = String.Empty
            _ordering = Enums.EpisodeOrdering.Standard
            _path = String.Empty
            _recursive = False
            _usefoldername = False
        End Sub

#End Region 'Methods

    End Class

#End Region 'Nested Types

End Class