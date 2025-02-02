import { Component, OnInit } from "@angular/core"
import { MatDialogModule, MatDialog } from "@angular/material/dialog"
import { VideoModalComponent } from "../video-modal/video-modal.component"
import { VideoStreamService } from '../../services/video-stream.service'
import { MatButtonModule } from '@angular/material/button'
import { MatCardModule } from '@angular/material/card'
import { MatGridListModule } from '@angular/material/grid-list'
import { CommonModule } from '@angular/common'

@Component({
  selector: "app-video-dashboard",
  templateUrl: "./video-dashboard.component.html",
  standalone: true,
  imports: [
    MatGridListModule,
    MatCardModule,
    MatDialogModule,
    MatButtonModule,
    CommonModule
  ],
  providers: [
    VideoStreamService
  ],
  styleUrls: ["./video-dashboard.component.scss"],
})
export class VideoDashboardComponent implements OnInit {
  cameras: string[] = []

  constructor(
    private videoStreamService: VideoStreamService,
    private dialog: MatDialog,
  ) { }

  ngOnInit() {
    this.loadCameras()
  }

  loadCameras() {
    this.videoStreamService.getCameraList().subscribe(
      (cameras) => (this.cameras = cameras),
      (error) => console.error("Error fetching cameras:", error),
    )
  }

  getMjpegUrl(cameraId: string): string {
    return this.videoStreamService.getMjpegStream(cameraId)
  }

  openVideoModal(cameraId: string) {
    this.dialog.open(VideoModalComponent, {
      width: "80%",
      height: "80%",
      data: { cameraId },
    })
  }
}

