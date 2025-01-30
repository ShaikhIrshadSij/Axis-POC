import { Inject, Injectable } from "@angular/core"
import { HttpClient } from "@angular/common/http"
import { Observable } from "rxjs"

@Injectable({
    providedIn: "root",
})
export class VideoStreamService {
    private apiUrl = "https://localhost:7293/api/videostream" // Replace with your actual API URL

    constructor(@Inject(HttpClient) private http: HttpClient) { }

    getMjpegStream(cameraId: string): string {
        return `${this.apiUrl}/${cameraId}/mjpeg`
    }

    getFullVideoStream(cameraId: string): Observable<Blob> {
        return this.http.get(`${this.apiUrl}/${cameraId}/full`, { responseType: "blob" })
    }

    getCameraList(): Observable<string[]> {
        return this.http.get<string[]>(`${this.apiUrl}/cameras`)
    }
}

